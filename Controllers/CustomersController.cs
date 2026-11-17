using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Customer records. Used by the Admin and by the retail outlet employees who register customers
/// while taking an enquiry or an order.
/// </summary>
[Authorize(Roles = "Admin,RetailEmployee")]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>Lists customers, newest first.</summary>
    public async Task<IActionResult> Index() =>
        View(await _db.Customers.OrderByDescending(c => c.Id).ToListAsync());

    /// <summary>Shows the details of one customer.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return View(c);
    }

    /// <summary>Shows the empty "add customer" form.</summary>
    public IActionResult Create() => View(new Customer());

    /// <summary>Saves a new customer after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (!ModelState.IsValid) return View(customer);

        // The customer portal finds the customer by e-mail, so an e-mail may belong to one customer only.
        if (!string.IsNullOrWhiteSpace(customer.Email) && await _db.Customers.AnyAsync(c => c.Email == customer.Email))
            ModelState.AddModelError(nameof(Customer.Email), "Another customer already uses this e-mail address.");
        if (!ModelState.IsValid) return View(customer);

        customer.CustomerCode = await SequenceHelper.NextCustomerCodeAsync(_db);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the edit form of a customer.</summary>
    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        return View(c);
    }

    /// <summary>Saves the changes of a customer after validating the input.
    /// Also keeps the Identity login (AspNetUsers) email in sync when the customer email changes.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.Id) return NotFound();
        if (!ModelState.IsValid) return View(customer);

        if (!string.IsNullOrWhiteSpace(customer.Email) &&
            await _db.Customers.AnyAsync(c => c.Email == customer.Email && c.Id != customer.Id))
        {
            ModelState.AddModelError(nameof(Customer.Email), "Another customer already uses this e-mail address.");
            return View(customer);
        }

        var existing = await _db.Customers.FindAsync(id);
        if (existing == null) return NotFound();

        var oldEmail = existing.Email?.Trim();
        var newEmail = customer.Email?.Trim();

        // Business fields
        existing.Name = customer.Name;
        existing.Phone = customer.Phone;
        existing.Email = customer.Email;
        existing.Address = customer.Address;
        existing.CityId = customer.CityId;
        existing.IsActive = customer.IsActive;

        await _db.SaveChangesAsync();

        // Sync Identity login email when it changed
        if (!string.IsNullOrWhiteSpace(oldEmail) &&
            !string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var loginUser = await _userManager.FindByEmailAsync(oldEmail);
            if (loginUser != null)
            {
                // Prevent taking over another existing login
                if (!string.IsNullOrWhiteSpace(newEmail) &&
                    await _userManager.FindByEmailAsync(newEmail) != null)
                {
                    // New email already used by another login — leave Identity as-is
                    // (optional: you can show a warning via TempData here)
                }
                else
                {
                    loginUser.Email = newEmail;
                    loginUser.UserName = newEmail;
                    loginUser.NormalizedEmail = _userManager.NormalizeEmail(newEmail);
                    loginUser.NormalizedUserName = _userManager.NormalizeName(newEmail);
                    loginUser.FullName = existing.Name;
                    await _userManager.UpdateAsync(loginUser);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(newEmail))
        {
            // Email unchanged — just keep FullName in sync
            var loginUser = await _userManager.FindByEmailAsync(newEmail);
            if (loginUser != null)
            {
                loginUser.FullName = existing.Name;
                await _userManager.UpdateAsync(loginUser);
            }
        }

        return RedirectToAction(nameof(Index));
    }
}