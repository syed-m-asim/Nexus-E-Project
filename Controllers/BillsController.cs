using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Bills. Only the accounts department (and the Admin) can generate bills; retail employees can only view them.
/// A bill contains the plan charge, the first-time security deposit, pending equipment replacements,
/// call usage, the bulk discount and 12.24% service tax.
/// </summary>
[Authorize(Roles = "Admin,Accounts,RetailEmployee")]
public class BillsController : Controller
{
    private readonly ApplicationDbContext _db;

    /// <summary>Service tax of 12.24% charged on the whole (discounted) bill.</summary>
    private const decimal TaxRate = 0.1224m;

    public BillsController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all bills, newest first.</summary>
    public async Task<IActionResult> Index()
    {
        // Retail employees only see bills of connections from their own store's orders.
        var storeId = await StoreScope.GetRetailStoreIdAsync(_db, User);
        var list = await _db.Bills
            .Include(b => b.Connection).ThenInclude(c => c!.Customer)
            .Where(b => storeId == null || b.Connection!.Order!.RetailStoreId == storeId)
            .OrderByDescending(b => b.BillDate).ToListAsync();
        return View(list);
    }

    /// <summary>Shows one bill with its line items and the payments received.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var bill = await _db.Bills
            .Include(b => b.Connection).ThenInclude(c => c!.Customer)
            .Include(b => b.LineItems)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bill == null) return NotFound();
        return View(bill);
    }

    /// <summary>Shows the "generate bill" form with the list of active connections.</summary>
    [Authorize(Roles = "Accounts,Admin")]
    public async Task<IActionResult> Generate()
    {
        ViewBag.ConnectionId = new SelectList(
            await _db.Connections.Include(c => c.Customer)
                .Where(c => c.Status == ConnectionStatus.Active)
                .Select(c => new { c.Id, Label = c.AccountId + " - " + c.Customer!.Name })
                .ToListAsync(), "Id", "Label");
        return View();
    }

    /// <summary>
    /// Generate bill including plan, deposit, bulk discount, pending replacements,
    /// optional call usage (local/STD/mobile minutes), and 12.24% service tax.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Accounts,Admin")]
    public async Task<IActionResult> Generate(int connectionId,
        int localMinutes = 0, int stdMinutes = 0, int mobileMinutes = 0)
    {
        var conn = await _db.Connections.Include(c => c.Plan).FirstOrDefaultAsync(c => c.Id == connectionId);
        if (conn == null) return NotFound();

        // VALIDATION (added): usage minutes, connection state and plan must be sensible before a bill is created.
        string? billError = null;
        if (localMinutes < 0 || stdMinutes < 0 || mobileMinutes < 0)
            billError = "Call minutes cannot be negative.";
        else if (localMinutes > 1000000 || stdMinutes > 1000000 || mobileMinutes > 1000000)
            billError = "Call minutes cannot be more than 1,000,000.";
        else if (conn.Status != ConnectionStatus.Active)
            billError = "Bills can only be generated for active connections.";
        else if (conn.Plan == null)
            billError = "The plan of this connection no longer exists.";
        else if (conn.ConnectionType != ConnectionType.TelephoneOnly && (localMinutes > 0 || stdMinutes > 0 || mobileMinutes > 0))
            billError = "Call usage can only be billed for telephone (landline) connections.";
        else if (await _db.Bills.AnyAsync(b => b.ConnectionId == connectionId && b.PeriodStart == DateTime.UtcNow.Date))
            billError = "A bill for this connection has already been generated today.";
        if (billError != null)
        {
            TempData["Error"] = billError;
            return RedirectToAction(nameof(Generate));
        }

        decimal planAmt = conn.Plan?.Price ?? 0;

        // Security deposit is charged once, on the first bill, after the bulk discount.
        bool firstBill = !conn.SecurityDepositBilled;   // the bulk discount applies to the advance (first bill) and the deposit
        decimal deposit = 0;
        if (!conn.SecurityDepositBilled)
        {
            deposit = conn.SecurityDepositAmount * (1 - conn.DiscountPercent / 100m);
            conn.SecurityDepositCharged = deposit;
            conn.SecurityDepositBilled = true;
        }

        // Pending replacement charges not yet billed
        var replacements = await _db.ReplacementCharges
            .Where(r => r.ConnectionId == connectionId && r.BillId == null)
            .ToListAsync();
        decimal replacementTotal = replacements.Sum(r => r.Amount);

        // Monthly rental of the equipment (modem / router) that is still with the customer.
        var rentals = await _db.ConnectionEquipments.Include(q => q.Product)
            .Where(q => q.ConnectionId == connectionId && q.ReturnedOn == null).ToListAsync();
        decimal rentalTotal = rentals.Sum(q => q.Product?.MonthlyRental ?? 0);

        // Unpaid balance of earlier bills (information on the new bill).
        decimal previousDue = await _db.Bills
            .Where(q => q.ConnectionId == connectionId && q.Status != BillStatus.Paid)
            .SumAsync(q => (decimal?)q.BalanceDue) ?? 0;

        // Call usage charges from plan rates
        decimal localRate = conn.Plan?.LocalCallRate ?? 0;
        decimal stdRate = conn.Plan?.StdCallRate ?? 0;
        decimal mobileRate = conn.Plan?.MobileMessageRate ?? 0;
        decimal usageLocal = localMinutes * localRate;
        decimal usageStd = stdMinutes * stdRate;
        decimal usageMobile = mobileMinutes * mobileRate;
        decimal usageTotal = usageLocal + usageStd + usageMobile;

        // Hourly plan usage not yet billed
        var hourUsages = await _db.UsageRecords
            .Where(u => u.ConnectionId == connectionId && u.BillId == null)
            .ToListAsync();
        decimal hourUsageTotal = 0;
        if (conn.Plan?.Hours is int included && included > 0 && hourUsages.Count > 0)
        {
            decimal extraHours = hourUsages.Sum(u => u.HoursUsed) - included;
            if (extraHours > 0)
                hourUsageTotal = Math.Round(extraHours * (planAmt / included), 2);
        }

        // Previous due is added to the payable total
        decimal discount = firstBill ? planAmt * (conn.DiscountPercent / 100m) : 0;
        decimal subtotal = planAmt + deposit + replacementTotal + usageTotal + rentalTotal + hourUsageTotal;
        decimal taxable = subtotal - discount;
        decimal tax = Math.Round(taxable * TaxRate, 2);
        decimal currentCharges = taxable + tax;
        decimal total = currentCharges + previousDue;

        var bill = new Bill
        {
            BillNumber = await SequenceHelper.NextBillNumberAsync(_db),
            ConnectionId = connectionId,
            PeriodStart = DateTime.UtcNow.Date,
            PeriodEnd = DateTime.UtcNow.Date.AddDays(conn.Plan?.ValidityDays ?? 30),
            BillDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(15),
            Subtotal = subtotal,
            DiscountAmount = discount,
            TaxableAmount = taxable,
            TaxRate = TaxRate,
            TaxAmount = tax,
            TotalAmount = total,
            PreviousDue = previousDue,
            BalanceDue = total,
            Status = BillStatus.Unpaid,
            GeneratedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User)
        };

        // Line items printed on the bill.
        bill.LineItems.Add(new BillLineItem
        {
            LineType = BillLineType.PlanCharge,
            Description = conn.Plan?.Name ?? "Plan charge",
            Quantity = 1, UnitPrice = planAmt, Amount = planAmt
        });
        if (deposit > 0)
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.SecurityDeposit,
                Description = "Security Deposit",
                Quantity = 1, UnitPrice = deposit, Amount = deposit
            });
        foreach (var rent in rentals)
        {
            var rentAmt = rent.Product?.MonthlyRental ?? 0;
            if (rentAmt > 0)
                bill.LineItems.Add(new BillLineItem
                {
                    LineType = BillLineType.EquipmentRental,
                    Description = "Equipment rental - " + (rent.Product?.Name ?? "equipment"),
                    Quantity = 1, UnitPrice = rentAmt, Amount = rentAmt
                });
        }
        if (usageLocal > 0)
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.UsageCharge,
                Description = $"Local calls {localMinutes} min @ {localRate:C}/min",
                Quantity = localMinutes, UnitPrice = localRate, Amount = usageLocal
            });
        if (usageStd > 0)
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.UsageCharge,
                Description = $"STD calls {stdMinutes} min @ {stdRate:C}/min",
                Quantity = stdMinutes, UnitPrice = stdRate, Amount = usageStd
            });
        if (usageMobile > 0)
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.UsageCharge,
                Description = $"Mobile messaging {mobileMinutes} min @ {mobileRate:C}/min",
                Quantity = mobileMinutes, UnitPrice = mobileRate, Amount = usageMobile
            });
        foreach (var r in replacements)
        {
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.ReplacementCharge,
                Description = r.Reason ?? "Equipment replacement",
                Quantity = r.Quantity, UnitPrice = r.Amount / Math.Max(1, r.Quantity), Amount = r.Amount
            });
        }
        if (discount > 0)
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.Discount,
                Description = $"Bulk discount {conn.DiscountPercent}%",
                Quantity = 1, UnitPrice = -discount, Amount = -discount
            });
        bill.LineItems.Add(new BillLineItem
        {
            LineType = BillLineType.Tax,
            Description = "Service Tax 12.24%",
            Quantity = 1, UnitPrice = tax, Amount = tax
        });
        if (hourUsageTotal > 0)
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.UsageCharge,
                Description = "Extra hourly usage",
                Quantity = 1, UnitPrice = hourUsageTotal, Amount = hourUsageTotal
            });
        if (previousDue > 0)
            bill.LineItems.Add(new BillLineItem
            {
                LineType = BillLineType.UsageCharge,
                Description = "Previous outstanding balance",
                Quantity = 1, UnitPrice = previousDue, Amount = previousDue
            });

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();

        foreach (var r in replacements)
            r.BillId = bill.Id;
        foreach (var u in hourUsages)
            u.BillId = bill.Id;
        await _db.SaveChangesAsync();

        await ApplyOverdueStatusAsync(connectionId);

        TempData["Success"] = $"Bill generated: {bill.BillNumber}";
        return RedirectToAction(nameof(Details), new { id = bill.Id });
    }

    private async Task ApplyOverdueStatusAsync(int connectionId)
    {
        var conn = await _db.Connections.FindAsync(connectionId);
        if (conn == null || conn.Status == ConnectionStatus.PermanentlyInactive) return;

        bool overdue = await _db.Bills.AnyAsync(b =>
            b.ConnectionId == connectionId &&
            b.Status != BillStatus.Paid &&
            b.DueDate < DateTime.UtcNow);

        if (overdue && conn.Status == ConnectionStatus.Active)
        {
            var old = conn.Status;
            conn.Status = ConnectionStatus.TemporarilyInactive;
            _db.ConnectionStatusHistories.Add(new ConnectionStatusHistory
            {
                ConnectionId = connectionId,
                OldStatus = old,
                NewStatus = ConnectionStatus.TemporarilyInactive,
                ChangedOn = DateTime.UtcNow,
                ChangedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User),
                Reason = "Automatic: unpaid bill past due date"
            });
            await _db.SaveChangesAsync();
        }
        else if (!overdue && conn.Status == ConnectionStatus.TemporarilyInactive)
        {
            var last = await _db.ConnectionStatusHistories
                .Where(h => h.ConnectionId == connectionId)
                .OrderByDescending(h => h.ChangedOn)
                .FirstOrDefaultAsync();
            if (last != null && last.Reason != null && last.Reason.StartsWith("Automatic:"))
            {
                var old = conn.Status;
                conn.Status = ConnectionStatus.Active;
                _db.ConnectionStatusHistories.Add(new ConnectionStatusHistory
                {
                    ConnectionId = connectionId,
                    OldStatus = old,
                    NewStatus = ConnectionStatus.Active,
                    ChangedOn = DateTime.UtcNow,
                    ChangedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User),
                    Reason = "Automatic: all bills paid / no longer overdue"
                });
                await _db.SaveChangesAsync();
            }
        }
    }
}
