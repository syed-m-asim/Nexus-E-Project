using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Connection orders: placing an order (retail staff / customers), tracking its status and
/// recording the feasibility checks (technical team).
/// Order ids look like D0000000001 (type letter + 10 digit serial).
/// </summary>
[Authorize(Roles = "Admin,RetailEmployee,Technical,Customer")]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    public OrdersController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all orders, newest first (staff only).</summary>
    [Authorize(Roles = "Admin,RetailEmployee,Technical")]
    public async Task<IActionResult> Index()
    {
        // Retail employees only see the orders of their own store.
        var storeId = await StoreScope.GetRetailStoreIdAsync(_db, User);
        var list = await _db.Orders
            .Include(o => o.Customer).Include(o => o.Plan).Include(o => o.RetailStore)
            .Where(o => storeId == null || o.RetailStoreId == storeId)
            .OrderByDescending(o => o.PlacedOn).ToListAsync();
        return View(list);
    }

    /// <summary>Lists the orders that still need technical work (placed, feasibility in progress or feasible).</summary>
    [Authorize(Roles = "Technical,Admin")]
    public async Task<IActionResult> Pending()
    {
        var list = await _db.Orders
            .Include(o => o.Customer).Include(o => o.Plan)
            .Where(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.FeasibilityInProgress || o.Status == OrderStatus.Feasible)
            .OrderBy(o => o.PlacedOn).ToListAsync();
        return View("Index", list);
    }

    /// <summary>Shows the full details of one order including the feasibility checks.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Customer).Include(o => o.Plan).Include(o => o.LandlinePlan)
            .Include(o => o.RetailStore).Include(o => o.City)
            .Include(o => o.FeasibilityChecks)
            .Include(o => o.EquipmentProduct)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        var scopeStore = await StoreScope.GetRetailStoreIdAsync(_db, User);
        if (scopeStore != null && order.RetailStoreId != scopeStore) return Forbid();
        if (User.IsInRole("Customer"))
        {
            var ownId = await _db.Customers
                .Where(c => c.Email == User.Identity!.Name)
                .Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (ownId == null || order.CustomerId != ownId.Value) return Forbid();
        }
        return View(order);
    }

    /// <summary>Shows the empty "place order" form.</summary>
    [Authorize(Roles = "RetailEmployee,Admin,Customer")]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();
        return View(new Order { NumberOfConnections = 1 });
    }

    /// <summary>
    /// Places a new order. The order number (type letter + 10 digit serial) and the bulk discount
    /// are generated here; the status always starts as "Placed".
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "RetailEmployee,Admin,Customer")]
    public async Task<IActionResult> Create(Order order)
    {
        // VALIDATION (added) - 1: attribute rules of the Order model (required fields, ranges, dial-up landline rule).
        // VALIDATION (added) - 2: database rules (customer / city / store / plan / equipment must exist and fit together).
        await OrderValidation.ValidateReferencesAsync(_db, order, ModelState);

        // VALIDATION (added) - 3: a logged-in customer may only place orders for his own customer record
        // (the customer is found through the login e-mail, which is the Identity user name).
        if (User.IsInRole("Customer"))
        {
            var ownId = await _db.Customers
                .Where(c => c.Email == User.Identity!.Name)
                .Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (ownId == null || ownId.Value != order.CustomerId)
                ModelState.AddModelError(nameof(Order.CustomerId), "You can only place orders for your own customer profile.");
        }

        // A retail employee can only place orders for his own store.
        var myStore = await StoreScope.GetRetailStoreIdAsync(_db, User);
        if (myStore != null && order.RetailStoreId != myStore)
            ModelState.AddModelError(nameof(Order.RetailStoreId), "You can only place orders for your own retail store.");

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(order);
        }

        var letter = order.ConnectionType switch
        {
            ConnectionType.DialUp => "D",
            ConnectionType.Broadband => "B",
            ConnectionType.TelephoneOnly => "T",
            _ => "X"
        };

        // Clash-free serial via SequenceCounters
        order.OrderNumber = await SequenceHelper.NextOrderNumberAsync(_db, letter);
        order.PlacedOn = DateTime.UtcNow;
        order.Status = OrderStatus.Placed;
        order.PlacedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User);

        // Bulk / corporate scheme: discount depends on the number of connections ordered.
        order.BulkDiscountPercent = await BulkDiscount.GetPercentAsync(_db, order.NumberOfConnections);

        // Dial-up: if existing landline, only internet feasibility later
        if (order.ConnectionType != ConnectionType.DialUp)
        {
            order.LandlinePlanId = null;
            order.ExistingLandlineConnectionId = null;
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Order placed. Order ID: {order.OrderNumber}";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    /// <summary>Shows the feasibility form of an order (technical team).</summary>
    [Authorize(Roles = "Technical,Admin")]
    [HttpGet]
    public async Task<IActionResult> Feasibility(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Customer).Include(o => o.Plan)
            .Include(o => o.FeasibilityChecks)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        return View(order);
    }

    /// <summary>
    /// Saves one feasibility check and updates the order status.
    /// Feasibility depends on distance and server availability, as per the company patterns.
    /// </summary>
    [Authorize(Roles = "Technical,Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Feasibility(int id, FeasibilityType checkType, double distanceKm,
        bool serverAvailable, bool isFeasible, string? remarks)
    {
        var order = await _db.Orders.Include(o => o.FeasibilityChecks).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        // VALIDATION (added): input checks before anything is saved.
        if (!Enum.IsDefined(typeof(FeasibilityType), checkType))
        {
            TempData["Error"] = "Select a valid check type.";
            return RedirectToAction(nameof(Feasibility), new { id });
        }
        if (double.IsNaN(distanceKm) || distanceKm < 0 || distanceKm > 10000)
        {
            TempData["Error"] = "Distance must be between 0 and 10,000 km.";
            return RedirectToAction(nameof(Feasibility), new { id });
        }
        if (remarks != null && remarks.Length > 500)
        {
            TempData["Error"] = "Remarks cannot be longer than 500 characters.";
            return RedirectToAction(nameof(Feasibility), new { id });
        }
        // Once connections are being provided, or the order is cancelled, no new feasibility check makes sense.
        if (order.Status == OrderStatus.ConnectionProvided || order.Status == OrderStatus.PartiallyProvided || order.Status == OrderStatus.Cancelled)
        {
            TempData["Error"] = "Feasibility cannot be recorded for an order that is already provided or cancelled.";
            return RedirectToAction(nameof(Feasibility), new { id });
        }
        // The check type must fit the connection type (see the rules in the comment below).
        bool typeAllowed = order.ConnectionType switch
        {
            ConnectionType.Broadband => checkType == FeasibilityType.Internet,
            ConnectionType.TelephoneOnly => checkType == FeasibilityType.Landline,
            // Dial-up with an existing landline needs the internet check only; a new dial-up needs both.
            ConnectionType.DialUp => order.ExistingLandlineConnectionId.HasValue
                ? checkType == FeasibilityType.Internet
                : true,
            _ => false
        };
        if (!typeAllowed)
        {
            TempData["Error"] = "This check type is not required for this connection type.";
            return RedirectToAction(nameof(Feasibility), new { id });
        }

        // Dial-up rules:
        // - New landline + internet: need both Landline and Internet checks
        // - Existing landline: only Internet check
        // - Broadband / TelephoneOnly: Internet or Landline type as applicable
        order.FeasibilityChecks.Add(new FeasibilityCheck
        {
            OrderId = id,
            CheckType = checkType,
            DistanceKm = distanceKm,
            ServerAvailable = serverAvailable,
            IsFeasible = isFeasible,
            Remarks = remarks,
            CheckedOn = DateTime.UtcNow,
            CheckedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User)
        });
        order.Status = OrderStatus.FeasibilityInProgress;

        var checks = order.FeasibilityChecks.ToList();
        bool allOk;
        if (order.ConnectionType == ConnectionType.DialUp)
        {
            if (order.ExistingLandlineConnectionId.HasValue)
            {
                // Only internet feasibility required
                allOk = checks.Any(c => c.CheckType == FeasibilityType.Internet && c.IsFeasible && c.ServerAvailable);
            }
            else
            {
                // Both landline and internet
                bool landOk = checks.Any(c => c.CheckType == FeasibilityType.Landline && c.IsFeasible && c.ServerAvailable);
                bool netOk = checks.Any(c => c.CheckType == FeasibilityType.Internet && c.IsFeasible && c.ServerAvailable);
                allOk = landOk && netOk;
            }
        }
        else if (order.ConnectionType == ConnectionType.TelephoneOnly)
        {
            allOk = checks.Any(c => c.CheckType == FeasibilityType.Landline && c.IsFeasible && c.ServerAvailable);
        }
        else
        {
            allOk = checks.Any(c => c.CheckType == FeasibilityType.Internet && c.IsFeasible && c.ServerAvailable);
        }

        if (!isFeasible)
            order.Status = OrderStatus.NotFeasible;
        else if (allOk)
            order.Status = OrderStatus.Feasible;

        if (!string.IsNullOrWhiteSpace(remarks))
            order.Remarks = (order.Remarks + " | " + remarks).Trim(' ', '|');

        await _db.SaveChangesAsync();
        TempData["Success"] = allOk ? "Feasibility complete — Order is Feasible." : "Feasibility check saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Manually changes the status of an order (technical team), optionally with a note.</summary>
    [Authorize(Roles = "Technical,Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? notes)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        // VALIDATION (added): valid status, note length and no changes on a finished order.
        if (!Enum.IsDefined(typeof(OrderStatus), status))
        {
            TempData["Error"] = "Select a valid order status.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (notes != null && notes.Length > 500)
        {
            TempData["Error"] = "Notes cannot be longer than 500 characters.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (order.Status == OrderStatus.ConnectionProvided || order.Status == OrderStatus.Cancelled)
        {
            TempData["Error"] = "The status of an order that is already provided or cancelled cannot be changed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        order.Status = status;
        if (!string.IsNullOrWhiteSpace(notes)) order.Remarks = notes;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Order status updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Fills the drop-down lists of the order form.</summary>
    private async Task LoadDropdowns()
    {
        if (User.IsInRole("Customer"))
        {
            var own = await _db.Customers.Where(c => c.Email == User.Identity!.Name).ToListAsync();
            ViewBag.CustomerId = new SelectList(own, "Id", "Name");
        }
        else
            ViewBag.CustomerId = new SelectList(await _db.Customers.ToListAsync(), "Id", "Name");
        ViewBag.PlanId = new SelectList(await _db.Plans.Where(p => p.IsActive).ToListAsync(), "Id", "Name");
        ViewBag.LandlinePlanId = new SelectList(
            await _db.Plans.Where(p => p.IsActive && p.ConnectionType == ConnectionType.TelephoneOnly).ToListAsync(), "Id", "Name");
        ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name");
        ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
        ViewBag.EquipmentProductId = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
        ViewBag.EquipmentImages = await _db.Products.Where(p => p.ImagePath != null).ToDictionaryAsync(p => p.Id, p => p.ImagePath!);   // pictures shown when equipment is selected
        ViewBag.ExistingLandlineConnectionId = new SelectList(
            await _db.Connections.Where(c => c.ConnectionType == ConnectionType.TelephoneOnly && c.Status == ConnectionStatus.Active)
                .Select(c => new { c.Id, Label = c.AccountId }).ToListAsync(), "Id", "Label");
    }
}
