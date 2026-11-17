using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>Product manufacturing vendors (suppliers of modems, routers etc.). Maintained by the Admin only.</summary>
[Authorize(Roles = "Admin")]
public class VendorsController : Controller
{
    private readonly ApplicationDbContext _db;
    public VendorsController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all vendors.</summary>
    public async Task<IActionResult> Index() => View(await _db.Vendors.ToListAsync());

    /// <summary>Shows the details of one vendor.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var v = await _db.Vendors.FindAsync(id);
        if (v == null) return NotFound();
        return View(v);
    }

    /// <summary>Shows the empty "add vendor" form.</summary>
    public IActionResult Create() => View(new Vendor());

    /// <summary>Saves a new vendor after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vendor v)
    {
        if (!ModelState.IsValid) return View(v);

        // VALIDATION (added): vendor names must be unique.
        if (await _db.Vendors.AnyAsync(x => x.Name == v.Name))
            ModelState.AddModelError(nameof(Vendor.Name), "A vendor with this name already exists.");
        if (!ModelState.IsValid) return View(v);

        _db.Vendors.Add(v);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vendor added.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the edit form of a vendor.</summary>
    public async Task<IActionResult> Edit(int id)
    {
        var v = await _db.Vendors.FindAsync(id);
        if (v == null) return NotFound();
        return View(v);
    }

    /// <summary>Saves the changes of a vendor after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vendor v)
    {
        if (id != v.Id) return NotFound();
        if (!ModelState.IsValid) return View(v);

        // VALIDATION (added): vendor name must stay unique (this vendor is excluded from the comparison).
        if (await _db.Vendors.AnyAsync(x => x.Name == v.Name && x.Id != v.Id))
            ModelState.AddModelError(nameof(Vendor.Name), "A vendor with this name already exists.");
        if (!ModelState.IsValid) return View(v);

        _db.Update(v);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vendor updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the delete confirmation page.</summary>
    public async Task<IActionResult> Delete(int id)
    {
        var v = await _db.Vendors.FindAsync(id);
        if (v == null) return NotFound();
        return View(v);
    }

    /// <summary>Deletes the vendor (only when no product or purchase order refers to it).</summary>
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var v = await _db.Vendors.FindAsync(id);

        // VALIDATION (added): a vendor with products or purchase orders cannot be deleted.
        if (v != null)
        {
            bool inUse = await _db.Products.AnyAsync(p => p.VendorId == id)
                || await _db.PurchaseOrders.AnyAsync(po => po.VendorId == id);
            if (inUse)
            {
                TempData["Error"] = "This vendor has products or purchase orders and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }
        }

        if (v != null) { _db.Vendors.Remove(v); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
