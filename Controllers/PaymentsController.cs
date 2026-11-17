using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Payments received against bills. Retail outlet employees record payments made at the shop,
/// the accounts department records payments made at the office.
/// </summary>
[Authorize(Roles = "Admin,Accounts,RetailEmployee")]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _db;
    public PaymentsController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all payments, newest first.</summary>
    public async Task<IActionResult> Index()
    {
        // Retail employees only see payments of their own store's connections.
        var storeId = await StoreScope.GetRetailStoreIdAsync(_db, User);
        var list = await _db.Payments.Include(p => p.Bill)
            .Where(p => storeId == null || p.Bill!.Connection!.Order!.RetailStoreId == storeId)
            .OrderByDescending(p => p.PaidOn).ToListAsync();
        return View(list);
    }

    /// <summary>Shows the "record payment" form with the list of unpaid bills.</summary>
    public async Task<IActionResult> Create()
    {
        var storeId = await StoreScope.GetRetailStoreIdAsync(_db, User);
        ViewBag.BillId = new SelectList(
            await _db.Bills.Where(b => b.Status != BillStatus.Paid
                    && (storeId == null || b.Connection!.Order!.RetailStoreId == storeId))
                .Select(b => new { b.Id, Label = b.BillNumber + " (Due: " + b.BalanceDue + ")" })
                .ToListAsync(), "Id", "Label");
        return View(new Payment());
    }

    /// <summary>Records a payment and updates the paid amount, balance and status of the bill.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Payment payment)
    {
        // Attribute rules (bill selected, amount greater than zero, valid mode) are defined on the Payment model.
        if (!ModelState.IsValid)
        {
            ViewBag.BillId = new SelectList(await _db.Bills.ToListAsync(), "Id", "BillNumber");
            return View(payment);
        }
        var bill = await _db.Bills.FindAsync(payment.BillId);
        if (bill == null) return NotFound();
        var myStore = await StoreScope.GetRetailStoreIdAsync(_db, User);
        if (myStore != null)
        {
            var billStore = await _db.Bills.Where(x => x.Id == bill.Id).Select(x => x.Connection!.Order!.RetailStoreId).FirstOrDefaultAsync();
            if (billStore != myStore) return Forbid();
        }

        // VALIDATION (added): business rules for a payment.
        if (bill.Status == BillStatus.Paid)
            ModelState.AddModelError(nameof(Payment.BillId), "This bill is already fully paid.");
        else if (payment.Amount > bill.BalanceDue)
            ModelState.AddModelError(nameof(Payment.Amount), $"Payment cannot be more than the balance due ({bill.BalanceDue:0.00}).");
        if (payment.Mode != PaymentMode.Cash && string.IsNullOrWhiteSpace(payment.Reference))
            ModelState.AddModelError(nameof(Payment.Reference), "Reference number is required for card, cheque and online payments.");
        if (!ModelState.IsValid)
        {
            ViewBag.BillId = new SelectList(
                await _db.Bills.Where(b => b.Status != BillStatus.Paid)
                    .Select(b => new { b.Id, Label = b.BillNumber + " (Due: " + b.BalanceDue + ")" })
                    .ToListAsync(), "Id", "Label", payment.BillId);
            return View(payment);
        }

        payment.PaidOn = DateTime.UtcNow;
        payment.ReceiptNumber = await SequenceHelper.NextReceiptNumberAsync(_db);
        payment.ReceivedByEmployeeId = await CurrentEmployee.GetIdAsync(_db, User);
        _db.Payments.Add(payment);

        bill.AmountPaid += payment.Amount;
        bill.BalanceDue = bill.TotalAmount - bill.AmountPaid;
        if (bill.BalanceDue < 0) bill.BalanceDue = 0;
        bill.Status = bill.BalanceDue <= 0 ? BillStatus.Paid :
                      bill.AmountPaid > 0 ? BillStatus.PartiallyPaid : BillStatus.Unpaid;

        await _db.SaveChangesAsync();
        await TryReactivateConnectionAsync(bill.ConnectionId);

        TempData["Success"] = "Payment recorded. Receipt: " + payment.ReceiptNumber;
        return RedirectToAction(nameof(Index));
    }

    private async Task TryReactivateConnectionAsync(int connectionId)
    {
        var conn = await _db.Connections.FindAsync(connectionId);
        if (conn == null || conn.Status != ConnectionStatus.TemporarilyInactive) return;

        bool stillOverdue = await _db.Bills.AnyAsync(b =>
            b.ConnectionId == connectionId &&
            b.Status != BillStatus.Paid &&
            b.DueDate < DateTime.UtcNow);
        if (stillOverdue) return;

        var last = await _db.ConnectionStatusHistories
            .Where(h => h.ConnectionId == connectionId)
            .OrderByDescending(h => h.ChangedOn)
            .FirstOrDefaultAsync();
        if (last == null || last.Reason == null || !last.Reason.StartsWith("Automatic:")) return;

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
