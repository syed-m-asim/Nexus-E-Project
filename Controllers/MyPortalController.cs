using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

[Authorize(Roles = "Customer")]
public class MyPortalController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MyPortalController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private async Task<Customer?> GetCustomer()
    {
        var email = (await _users.GetUserAsync(User))?.Email;
        if (string.IsNullOrEmpty(email)) return null;
        return await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);
    }

    public async Task<IActionResult> Index()
    {
        var customer = await GetCustomer();
        ViewBag.Customer = customer;
        if (customer != null)
        {
            ViewBag.OrderCount = await _db.Orders.CountAsync(o => o.CustomerId == customer.Id);
            ViewBag.ConnectionCount = await _db.Connections.CountAsync(c => c.CustomerId == customer.Id);
            ViewBag.BillCount = await _db.Bills.Include(b => b.Connection)
                .CountAsync(b => b.Connection!.CustomerId == customer.Id);
        }
        return View();
    }

    public async Task<IActionResult> Orders()
    {
        var customer = await GetCustomer();
        if (customer == null) return View(new List<Order>());
        return View(await _db.Orders.Include(o => o.Plan)
            .Where(o => o.CustomerId == customer.Id).OrderByDescending(o => o.PlacedOn).ToListAsync());
    }

    public async Task<IActionResult> Connections()
    {
        var customer = await GetCustomer();
        if (customer == null) return View(new List<Connection>());
        return View(await _db.Connections.Include(c => c.Plan)
            .Where(c => c.CustomerId == customer.Id).OrderByDescending(c => c.ActivatedOn).ToListAsync());
    }

    public async Task<IActionResult> Bills()
    {
        var customer = await GetCustomer();
        if (customer == null) return View(new List<Bill>());
        return View(await _db.Bills.Include(b => b.Connection)
            .Where(b => b.Connection!.CustomerId == customer.Id).OrderByDescending(b => b.BillDate).ToListAsync());
    }

    public async Task<IActionResult> Profile()
    {
        var customer = await GetCustomer();
        if (customer == null)
        {
            TempData["Error"] = "No customer profile linked to your login email. Visit a retail outlet to register.";
            return RedirectToAction(nameof(Index));
        }
        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var customer = await GetCustomer();
        if (customer == null)
        {
            TempData["Error"] = "No customer profile linked to your login email.";
            return RedirectToAction(nameof(Index));
        }
        return View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(Customer posted)
    {
        var customer = await GetCustomer();
        if (customer == null) return RedirectToAction(nameof(Index));

        customer.Name = posted.Name;
        customer.Phone = posted.Phone;
        customer.Address = posted.Address;

        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(customer.Name) || customer.Name.Length < 2)
            ModelState.AddModelError(nameof(Customer.Name), "Customer name is required.");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(customer.Name, @"^[A-Za-z]+( [A-Za-z]+)*$"))
            ModelState.AddModelError(nameof(Customer.Name), "Customer name can contain only letters and single spaces.");
        if (string.IsNullOrWhiteSpace(customer.Phone))
            ModelState.AddModelError(nameof(Customer.Phone), "Contact number is required.");
        if (string.IsNullOrWhiteSpace(customer.Address))
            ModelState.AddModelError(nameof(Customer.Address), "Address is required.");
        if (!ModelState.IsValid) return View(customer);

        _db.Update(customer);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Your contact details have been updated.";
        return RedirectToAction(nameof(Profile));
    }

    public async Task<IActionResult> PlaceOrder()
    {
        var customer = await GetCustomer();
        if (customer == null)
        {
            TempData["Error"] = "No customer profile linked to your login email. Visit a retail outlet to register.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.PlanId = new SelectList(await _db.Plans.Where(p => p.IsActive).ToListAsync(), "Id", "Name");
        ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
        ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name");
        ViewBag.EquipmentProductId = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
        ViewBag.EquipmentImages = await _db.Products.Where(p => p.ImagePath != null).ToDictionaryAsync(p => p.Id, p => p.ImagePath!);   // pictures shown when equipment is selected
        await LoadLandlineLists(customer.Id);
        return View(new Order { CustomerId = customer.Id, NumberOfConnections = 1 });
    }

    private async Task LoadLandlineLists(int customerId)
    {
        ViewBag.LandlinePlanId = new SelectList(
            await _db.Plans.Where(p => p.IsActive && p.ConnectionType == ConnectionType.TelephoneOnly).ToListAsync(), "Id", "Name");
        ViewBag.ExistingLandlineConnectionId = new SelectList(
            await _db.Connections.Where(c => c.CustomerId == customerId && c.ConnectionType == ConnectionType.TelephoneOnly && c.Status == ConnectionStatus.Active)
                .Select(c => new { c.Id, Label = c.AccountId }).ToListAsync(), "Id", "Label");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(Order order)
    {
        var customer = await GetCustomer();
        if (customer == null) return RedirectToAction(nameof(Index));
        order.CustomerId = customer.Id;

        ModelState.Remove(nameof(Order.CustomerId));
        await OrderValidation.ValidateReferencesAsync(_db, order, ModelState);
        if (!ModelState.IsValid)
        {
            ViewBag.PlanId = new SelectList(await _db.Plans.Where(p => p.IsActive).ToListAsync(), "Id", "Name");
            ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
            ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name");
            ViewBag.EquipmentProductId = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
            ViewBag.EquipmentImages = await _db.Products.Where(p => p.ImagePath != null).ToDictionaryAsync(p => p.Id, p => p.ImagePath!);   // pictures shown when equipment is selected
            await LoadLandlineLists(customer.Id);
            return View(order);
        }

        var letter = order.ConnectionType switch
        {
            ConnectionType.DialUp => "D",
            ConnectionType.Broadband => "B",
            ConnectionType.TelephoneOnly => "T",
            _ => "X"
        };
        order.OrderNumber = await SequenceHelper.NextOrderNumberAsync(_db, letter);
        order.PlacedOn = DateTime.UtcNow;
        order.Status = OrderStatus.Placed;
        order.PlacedByEmployeeId = 0;
        if (order.ConnectionType != ConnectionType.DialUp)
        {
            order.LandlinePlanId = null;
            order.ExistingLandlineConnectionId = null;
        }
        order.BulkDiscountPercent = await BulkDiscount.GetPercentAsync(_db, order.NumberOfConnections);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Order placed! Your Order ID: {order.OrderNumber}";
        return RedirectToAction(nameof(Orders));
    }
}
