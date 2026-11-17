using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Equipment (modems, routers, handsets) and the stock available.
/// Admin can add / edit / delete products, the technical team can view and edit them (stock maintenance).
/// </summary>
[Authorize(Roles = "Admin,Technical")]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;   // needed to save / delete the picture files in wwwroot
    public ProductsController(ApplicationDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    /// <summary>Lists all products with their vendor and stock.</summary>
    public async Task<IActionResult> Index() =>
        View(await _db.Products.Include(p => p.Vendor).ToListAsync());

    /// <summary>Shows the details of one product.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var p = await _db.Products.Include(x => x.Vendor).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        return View(p);
    }

    /// <summary>Shows the empty "add product" form (Admin only).</summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name");
        return View(new Product());
    }

    /// <summary>Saves a new product after validating the input (Admin only).</summary>
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Product p, IFormFile? imageFile)
    {
        // Optional picture: must be a real JPG / PNG / GIF / WEBP of at most 2 MB.
        var imgError = await ProductImageStorage.ValidateAsync(imageFile);
        if (imgError != null) ModelState.AddModelError("imageFile", imgError);

        if (!ModelState.IsValid)
        {
            ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name");
            return View(p);
        }

        // VALIDATION (added): the selected vendor must exist.
        if (p.VendorId.HasValue && !await _db.Vendors.AnyAsync(v => v.Id == p.VendorId.Value))
            ModelState.AddModelError(nameof(Product.VendorId), "The selected vendor does not exist.");
        if (!ModelState.IsValid)
        {
            ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name");
            return View(p);
        }

        if (imageFile != null && imageFile.Length > 0)
            p.ImagePath = await ProductImageStorage.SaveAsync(imageFile, _env);

        _db.Products.Add(p);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the edit form of a product.</summary>
    [Authorize(Roles = "Admin,Technical")]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();
        ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name", p.VendorId);
        return View(p);
    }

    /// <summary>Saves the changes of a product after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Technical")]
    public async Task<IActionResult> Edit(int id, Product p, IFormFile? imageFile, bool removeImage)
    {
        if (id != p.Id) return NotFound();

        // The picture path is never posted by the form: keep the stored one unless a new picture is chosen / removed.
        var oldPath = await _db.Products.AsNoTracking().Where(x => x.Id == id).Select(x => x.ImagePath).FirstOrDefaultAsync();
        p.ImagePath = oldPath;
        var imgError = await ProductImageStorage.ValidateAsync(imageFile);
        if (imgError != null) ModelState.AddModelError("imageFile", imgError);

        if (!ModelState.IsValid)
        {
            ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name");
            return View(p);
        }

        // VALIDATION (added): the selected vendor must exist.
        if (p.VendorId.HasValue && !await _db.Vendors.AnyAsync(v => v.Id == p.VendorId.Value))
            ModelState.AddModelError(nameof(Product.VendorId), "The selected vendor does not exist.");
        if (!ModelState.IsValid)
        {
            ViewBag.VendorId = new SelectList(await _db.Vendors.ToListAsync(), "Id", "Name", p.VendorId);
            return View(p);
        }

        if (imageFile != null && imageFile.Length > 0)
            p.ImagePath = await ProductImageStorage.SaveAsync(imageFile, _env);   // new picture replaces the old one
        else if (removeImage)
            p.ImagePath = null;

        _db.Update(p);
        await _db.SaveChangesAsync();

        // The old file is deleted only after the database update succeeded.
        if (p.ImagePath != oldPath) ProductImageStorage.Delete(oldPath, _env);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the delete confirmation page (Admin only).</summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();
        return View(p);
    }

    /// <summary>Deletes the product (only when it is not used anywhere).</summary>
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var p = await _db.Products.FindAsync(id);

        // VALIDATION (added): equipment that was given to customers, ordered or purchased cannot be deleted.
        if (p != null)
        {
            bool inUse = await _db.ConnectionEquipments.AnyAsync(e => e.ProductId == id)
                || await _db.PurchaseOrderItems.AnyAsync(i => i.ProductId == id)
                || await _db.ReplacementCharges.AnyAsync(r => r.ProductId == id)
                || await _db.Orders.AnyAsync(o => o.EquipmentProductId == id);
            if (inUse)
            {
                TempData["Error"] = "This product is used by connections, orders or purchase orders and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }
        }

        if (p != null)
        {
            var imagePath = p.ImagePath;
            _db.Products.Remove(p);
            await _db.SaveChangesAsync();
            ProductImageStorage.Delete(imagePath, _env);   // remove the picture file too
        }
        return RedirectToAction(nameof(Index));
    }
}
