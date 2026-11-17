using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Purchase lists / orders placed with product manufacturing vendors (Admin only).
/// Receiving a purchase order adds the quantities to the stock of the products.
/// </summary>
[Authorize(Roles = "Admin")]
public class PurchaseOrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    public PurchaseOrdersController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all purchase orders, newest first.</summary>
    public async Task<IActionResult> Index()
    {
        var list = await _db.PurchaseOrders
            .Include(p => p.Vendor)
            .Include(p => p.Items)
            .OrderByDescending(p => p.OrderDate)
            .ToListAsync();
        return View(list);
    }

    /// <summary>Shows one purchase order with its items and vendor payments.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.Vendor)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return NotFound();
        return View(po);
    }

    /// <summary>Shows the empty "new purchase order" form.</summary>
    public async Task<IActionResult> Create()
    {
        ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name");
        ViewBag.ProductId = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
        return View(new PurchaseOrder());
    }

    /// <summary>Creates a purchase order with one product line. The PO number is generated here.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseOrder po, int productId, int quantity, decimal unitCost)
    {
        if (productId <= 0 || quantity <= 0)
        {
            ModelState.AddModelError("", "Select a product and quantity.");
            ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name");
            ViewBag.ProductId = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
            return View(po);
        }

        // VALIDATION (added): vendor selected (model rule), vendor / product exist, sensible quantity and cost.
        if (quantity > 100000)
            ModelState.AddModelError("", "Quantity cannot be more than 100,000.");
        if (unitCost <= 0 || unitCost > 10000000)
            ModelState.AddModelError("", "Unit cost must be greater than zero and not more than 10,000,000.");
        if (po.VendorId > 0 && !await _db.Vendors.AnyAsync(v => v.Id == po.VendorId))
            ModelState.AddModelError(nameof(PurchaseOrder.VendorId), "The selected vendor does not exist.");
        if (!await _db.Products.AnyAsync(p => p.Id == productId))
            ModelState.AddModelError("", "The selected product does not exist.");
        if (!ModelState.IsValid)
        {
            ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name");
            ViewBag.ProductId = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
            return View(po);
        }

        var count = await _db.PurchaseOrders.CountAsync() + 1;
        po.PoNumber = await SequenceHelper.NextPoNumberAsync(_db);
        po.OrderDate = DateTime.UtcNow;
        po.Status = PurchaseOrderStatus.Ordered;
        po.Items.Add(new PurchaseOrderItem
        {
            ProductId = productId,
            Quantity = quantity,
            UnitCost = unitCost
        });
        po.TotalAmount = quantity * unitCost;

        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Purchase Order {po.PoNumber} created.";
        return RedirectToAction(nameof(Details), new { id = po.Id });
    }

    /// <summary>Marks the purchase order as received and adds the quantities to the product stock.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int id)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return NotFound();
        if (po.Status == PurchaseOrderStatus.Received)
        {
            TempData["Error"] = "Already received.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // VALIDATION (added): a cancelled purchase order cannot be received.
        if (po.Status == PurchaseOrderStatus.Cancelled)
        {
            TempData["Error"] = "A cancelled purchase order cannot be received.";
            return RedirectToAction(nameof(Details), new { id });
        }

        foreach (var item in po.Items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product != null)
                product.StockQty += item.Quantity;
        }
        po.Status = PurchaseOrderStatus.Received;
        po.ReceivedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Stock updated. PO marked Received.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Records a payment made to the vendor against this purchase order.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(int id, decimal amount, PaymentMode mode, string? reference)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po == null) return NotFound();

        // VALIDATION (added): positive amount, not more than the outstanding amount, valid mode and reference.
        string? payError = null;
        if (po.Status == PurchaseOrderStatus.Cancelled)
            payError = "A payment cannot be recorded for a cancelled purchase order.";
        else if (amount <= 0)
            payError = "Payment amount must be greater than zero.";
        else if (amount > po.TotalAmount - po.AmountPaid)
            payError = $"Payment cannot be more than the outstanding amount ({po.TotalAmount - po.AmountPaid:0.00}).";
        else if (!Enum.IsDefined(typeof(PaymentMode), mode))
            payError = "Select a valid payment mode.";
        else if (mode != PaymentMode.Cash && string.IsNullOrWhiteSpace(reference))
            payError = "Reference number is required for card, cheque and online payments.";
        else if (reference != null && reference.Length > 100)
            payError = "Reference cannot be longer than 100 characters.";
        if (payError != null)
        {
            TempData["Error"] = payError;
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.VendorPayments.Add(new VendorPayment
        {
            PurchaseOrderId = id,
            Amount = amount,
            PaidOn = DateTime.UtcNow,
            Mode = mode,
            Reference = reference
        });
        po.AmountPaid += amount;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vendor payment recorded.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Shows the delete confirmation page.</summary>
    public async Task<IActionResult> Delete(int id)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Vendor).FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return NotFound();
        return View(po);
    }

    /// <summary>Deletes a purchase order together with its items and payments.</summary>
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Items).Include(p => p.Payments).FirstOrDefaultAsync(p => p.Id == id);

        // VALIDATION (added): a received purchase order has already increased the stock, so it must stay on record.
        if (po != null && po.Status == PurchaseOrderStatus.Received)
        {
            TempData["Error"] = "A received purchase order cannot be deleted because the stock has already been updated.";
            return RedirectToAction(nameof(Index));
        }

        if (po != null)
        {
            _db.PurchaseOrderItems.RemoveRange(po.Items);
            _db.VendorPayments.RemoveRange(po.Payments);
            _db.PurchaseOrders.Remove(po);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
