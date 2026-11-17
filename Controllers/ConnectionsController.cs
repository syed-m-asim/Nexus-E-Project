using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Provided connections. The technical team creates a connection from a feasible order, changes its
/// status (active / temporarily inactive / permanently inactive) and records equipment replacements.
/// The account id is 16 characters: type letter + 3 digit city code + 12 digit serial.
/// </summary>
[Authorize(Roles = "Admin,Technical,Accounts,RetailEmployee")]
public class ConnectionsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ConnectionsController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all connections, newest first.</summary>
    public async Task<IActionResult> Index()
    {
        // Retail employees only see connections created from orders of their own store.
        var storeId = await StoreScope.GetRetailStoreIdAsync(_db, User);
        var list = await _db.Connections
            .Include(c => c.Customer).Include(c => c.Plan)
            .Where(c => storeId == null || c.Order!.RetailStoreId == storeId)
            .OrderByDescending(c => c.ActivatedOn).ToListAsync();
        return View(list);
    }

    /// <summary>Shows the details of a connection: bills, plan changes, replacements and the action forms.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var c = await _db.Connections
            .Include(x => x.Customer).Include(x => x.Plan).Include(x => x.Bills)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        // Status history (active / temporarily inactive / permanently inactive), newest first.
        var history = await _db.ConnectionStatusHistories
            .Where(h => h.ConnectionId == id)
            .OrderByDescending(h => h.ChangedOn).ToListAsync();
        ViewBag.StatusHistory = history;

        // Employee names for the "Changed by" column of the status history.
        var empIds = history.Where(h => h.ChangedByEmployeeId != null)
            .Select(h => h.ChangedByEmployeeId!.Value).Distinct().ToList();
        ViewBag.EmployeeNames = await _db.Employees
            .Where(e => empIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name);

        ViewBag.PlanChanges = await _db.ConnectionPlanChanges
            .Where(p => p.ConnectionId == id).OrderByDescending(p => p.ChangedOn).ToListAsync();
        ViewBag.Replacements = await _db.ReplacementCharges
            .Include(r => r.Product)
            .Where(r => r.ConnectionId == id).OrderByDescending(r => r.CreatedOn).ToListAsync();
        ViewBag.Plans = new SelectList(
            await _db.Plans.Where(p => p.IsActive && p.ConnectionType == c.ConnectionType).ToListAsync(), "Id", "Name");
        ViewBag.Products = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
        return View(c);
    }

    /// <summary>
    /// Creates a connection (and its 16 character account id) from a feasible order,
    /// reduces the stock of the chosen equipment and marks the order as provided.
    /// </summary>
    [Authorize(Roles = "Technical,Admin")]
    public async Task<IActionResult> CreateFromOrder(int orderId)
    {
        var order = await _db.Orders
            .Include(o => o.Customer).Include(o => o.Plan).Include(o => o.City)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return NotFound();
        if (order.Status != OrderStatus.Feasible && order.Status != OrderStatus.PartiallyProvided)
        {
            TempData["Error"] = "Order must be Feasible before creating connection.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        // VALIDATION (added): never create more connections than the order asked for, and the plan must still exist.
        if (order.ConnectionsProvided >= order.NumberOfConnections)
        {
            TempData["Error"] = "All connections requested in this order have already been created.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }
        if (order.Plan == null)
        {
            TempData["Error"] = "The plan of this order no longer exists. Please correct the order first.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        // City code (3 digits) is part of the account id; "001" is used if the city is missing.
        var cityCode = order.City?.Code ?? "001";

        // Refundable security deposit for this connection type.
        var deposit = await _db.SecurityDepositRates
            .Where(r => r.ConnectionType == order.ConnectionType)
            .Select(r => r.Amount).FirstOrDefaultAsync();

        var conn = new Connection
        {
            AccountId = await NextAccountIdAsync(order.ConnectionType, cityCode),
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            CityId = order.CityId,
            ConnectionType = order.ConnectionType,
            PlanId = order.PlanId,
            Status = ConnectionStatus.Active,
            ActivatedOn = DateTime.UtcNow,
            DiscountPercent = order.BulkDiscountPercent,
            SecurityDepositAmount = deposit,
            LinkedLandlineConnectionId = order.ExistingLandlineConnectionId,
            CreatedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User)
        };

        // Dial-up with a NEW landline: the landline connection (type T) is created together with the internet connection.
        Connection? newLandline = null;
        if (order.ConnectionType == ConnectionType.DialUp && order.LandlinePlanId.HasValue)
        {
            var landlinePlan = await _db.Plans.FindAsync(order.LandlinePlanId.Value);
            if (landlinePlan != null)
            {
                var landlineDeposit = await _db.SecurityDepositRates
                    .Where(r => r.ConnectionType == ConnectionType.TelephoneOnly)
                    .Select(r => r.Amount).FirstOrDefaultAsync();
                newLandline = new Connection
                {
                    AccountId = await NextAccountIdAsync(ConnectionType.TelephoneOnly, cityCode),
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    CityId = order.CityId,
                    ConnectionType = ConnectionType.TelephoneOnly,
                    PlanId = landlinePlan.Id,
                    Status = ConnectionStatus.Active,
                    ActivatedOn = DateTime.UtcNow,
                    DiscountPercent = order.BulkDiscountPercent,
                    SecurityDepositAmount = landlineDeposit,
                    CreatedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User)
                };
                _db.Connections.Add(newLandline);
                await _db.SaveChangesAsync();           // the landline needs an Id before it can be linked
                conn.LinkedLandlineConnectionId = newLandline.Id;
            }
        }
        _db.Connections.Add(conn);

        // Hand over the chosen equipment (if any) and reduce the stock by one unit.
        if (order.EquipmentProductId.HasValue)
        {
            var prod = await _db.Products.FindAsync(order.EquipmentProductId.Value);
            if (prod != null && prod.StockQty > 0)
            {
                prod.StockQty--;
                conn.Equipment.Add(new ConnectionEquipment { ProductId = prod.Id, AssignedOn = DateTime.UtcNow });
            }
        }

        // Bulk orders: the order stays "PartiallyProvided" until all requested connections are created.
        order.ConnectionsProvided++;
        order.Status = order.ConnectionsProvided >= order.NumberOfConnections
            ? OrderStatus.ConnectionProvided
            : OrderStatus.PartiallyProvided;
        await _db.SaveChangesAsync();

        TempData["Success"] = newLandline == null
            ? $"Connection created. Account ID: {conn.AccountId}"
            : $"Connection created. Account ID: {conn.AccountId} (new landline: {newLandline.AccountId})";
        return RedirectToAction(nameof(Details), new { id = conn.Id });
    }

    /// <summary>Next 16 character account id (clash-free via SequenceCounters).</summary>
    private async Task<string> NextAccountIdAsync(ConnectionType type, string cityCode)
    {
        return await SequenceHelper.NextAccountIdAsync(_db, type.Letter().ToString(), cityCode);
    }

    /// <summary>Refunds the security deposit of a permanently inactive (withdrawn / cancelled) connection.</summary>
    [Authorize(Roles = "Accounts,Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id)
    {
        var c = await _db.Connections.FindAsync(id);
        if (c == null) return NotFound();
        if (c.Status != ConnectionStatus.PermanentlyInactive || !c.SecurityDepositBilled || c.RefundAmount != null)
        {
            TempData["Error"] = "A deposit can be refunded once, only for a permanently inactive connection whose deposit was billed.";
            return RedirectToAction(nameof(Details), new { id });
        }
        c.RefundAmount = c.SecurityDepositCharged;
        c.RefundedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Security deposit of {c.RefundAmount:0.00} refunded.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Makes a connection active, temporarily inactive or permanently inactive and writes a history record.</summary>
    [Authorize(Roles = "Technical,Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, ConnectionStatus status, string? reason)
    {
        var c = await _db.Connections.FindAsync(id);
        if (c == null) return NotFound();

        // VALIDATION (added): valid status, an actual change, and a permanently closed connection stays closed.
        if (!Enum.IsDefined(typeof(ConnectionStatus), status))
        {
            TempData["Error"] = "Select a valid connection status.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (reason != null && reason.Length > 300)
        {
            TempData["Error"] = "Reason cannot be longer than 300 characters.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (c.Status == status)
        {
            TempData["Error"] = "The connection already has this status.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (c.Status == ConnectionStatus.PermanentlyInactive)
        {
            TempData["Error"] = "A permanently inactive connection cannot be changed again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var old = c.Status;
        c.Status = status;
        c.DeactivatedOn = status != ConnectionStatus.Active ? DateTime.UtcNow : null;
        _db.ConnectionStatusHistories.Add(new ConnectionStatusHistory
        {
            ConnectionId = id,
            OldStatus = old,
            NewStatus = status,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ChangedOn = DateTime.UtcNow,
            ChangedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User)
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Connection status updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Changes the plan of a connection and records the change in the plan history.</summary>
    [Authorize(Roles = "Technical,Admin,RetailEmployee")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(int id, int newPlanId, string? reason)
    {
        var c = await _db.Connections.FindAsync(id);
        if (c == null) return NotFound();
        var newPlan = await _db.Plans.FindAsync(newPlanId);
        if (newPlan == null) return BadRequest();

        // VALIDATION (added): the new plan must be active, of the same connection type and different from the current one.
        string? planError = null;
        if (c.Status == ConnectionStatus.PermanentlyInactive)
            planError = "The plan of a permanently inactive connection cannot be changed.";
        else if (!newPlan.IsActive)
            planError = "The selected plan is not active.";
        else if (newPlan.ConnectionType != c.ConnectionType)
            planError = "The selected plan belongs to a different connection type.";
        else if (newPlan.Id == c.PlanId)
            planError = "The connection is already on this plan.";
        else if (reason != null && reason.Length > 300)
            planError = "Reason cannot be longer than 300 characters.";
        if (planError != null)
        {
            TempData["Error"] = planError;
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.ConnectionPlanChanges.Add(new ConnectionPlanChange
        {
            ConnectionId = id,
            OldPlanId = c.PlanId,
            NewPlanId = newPlanId,
            Reason = reason,
            ChangedOn = DateTime.UtcNow,
            ChangedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User)
        });
        c.PlanId = newPlanId;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Plan changed to {newPlan.Name}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Records a replacement of spoiled equipment; the charge is added to the next bill.</summary>
    [Authorize(Roles = "Technical,Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReplacement(int id, int productId, int quantity, string? reason)
    {
        var c = await _db.Connections.FindAsync(id);
        if (c == null) return NotFound();

        if (productId <= 0)
        {
            TempData["Error"] = "Please select a product.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var product = await _db.Products.FindAsync(productId);
        if (product == null)
        {
            TempData["Error"] = "Selected product was not found.";
            return RedirectToAction(nameof(Details), new { id });
        }
        // VALIDATION (added): sensible quantity, enough stock, reason length and an open connection.
        string? replError = null;
        if (quantity < 1 || quantity > 100)
            replError = "Quantity must be between 1 and 100.";
        else if (quantity > product.StockQty)
            replError = $"Only {product.StockQty} unit(s) of this product are in stock.";
        else if (reason != null && reason.Length > 300)
            replError = "Reason cannot be longer than 300 characters.";
        else if (c.Status == ConnectionStatus.PermanentlyInactive)
            replError = "A replacement cannot be recorded for a permanently inactive connection.";
        if (replError != null)
        {
            TempData["Error"] = replError;
            return RedirectToAction(nameof(Details), new { id });
        }

        var amount = product.ReplacementCharge * Math.Max(1, quantity);
        _db.ReplacementCharges.Add(new ReplacementCharge
        {
            ConnectionId = id,
            ProductId = productId,
            Quantity = quantity,
            Amount = amount,
            Reason = reason,
            CreatedOn = DateTime.UtcNow
        });
        if (product.StockQty >= quantity)
            product.StockQty -= quantity;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Replacement charge {amount:C} recorded (will be on next bill).";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Record hourly usage for a connection (Technical / Accounts).</summary>
    [Authorize(Roles = "Technical,Accounts,Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordUsage(int id, decimal hoursUsed, DateTime periodStart, DateTime periodEnd, string? notes)
    {
        var c = await _db.Connections.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (c.Plan?.Hours == null)
        {
            TempData["Error"] = "This connection is not on an hourly plan.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (hoursUsed <= 0)
        {
            TempData["Error"] = "Hours used must be greater than zero.";
            return RedirectToAction(nameof(Details), new { id });
        }
        _db.UsageRecords.Add(new UsageRecord
        {
            ConnectionId = id,
            HoursUsed = hoursUsed,
            PeriodStart = periodStart == default ? DateTime.UtcNow.Date.AddDays(-30) : periodStart,
            PeriodEnd = periodEnd == default ? DateTime.UtcNow.Date : periodEnd,
            RecordedOn = DateTime.UtcNow,
            RecordedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User),
            Notes = notes
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Recorded {hoursUsed} hours of usage.";
        return RedirectToAction(nameof(Details), new { id });
    }
}