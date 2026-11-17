using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>Retail outlets of the company, one or more per city. Maintained by the Admin only.</summary>
[Authorize(Roles = "Admin")]
public class RetailStoresController : Controller
{
    private readonly ApplicationDbContext _db;
    public RetailStoresController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all retail stores with their city.</summary>
    public async Task<IActionResult> Index() =>
        View(await _db.RetailStores.Include(r => r.City).ToListAsync());

    /// <summary>Shows the empty "add store" form (with the city drop-down).</summary>
    public async Task<IActionResult> Create()
    {
        ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
        return View(new RetailStore());
    }

    /// <summary>Saves a new store after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RetailStore store)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
            return View(store);
        }

        // VALIDATION (added): the city must exist and the store name must be unique inside the city.
        if (!await _db.Cities.AnyAsync(c => c.Id == store.CityId))
            ModelState.AddModelError(nameof(RetailStore.CityId), "The selected city does not exist.");
        if (await _db.RetailStores.AnyAsync(s => s.Name == store.Name && s.CityId == store.CityId))
            ModelState.AddModelError(nameof(RetailStore.Name), "A store with this name already exists in the selected city.");
        if (!ModelState.IsValid)
        {
            ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
            return View(store);
        }

        _db.RetailStores.Add(store);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the edit form of a store.</summary>
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _db.RetailStores.FindAsync(id);
        if (s == null) return NotFound();
        ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name", s.CityId);
        return View(s);
    }

    /// <summary>Saves the changes of a store after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RetailStore store)
    {
        if (id != store.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
            return View(store);
        }

        // VALIDATION (added): same rules as Create (this store is excluded from the duplicate check).
        if (!await _db.Cities.AnyAsync(c => c.Id == store.CityId))
            ModelState.AddModelError(nameof(RetailStore.CityId), "The selected city does not exist.");
        if (await _db.RetailStores.AnyAsync(s => s.Name == store.Name && s.CityId == store.CityId && s.Id != store.Id))
            ModelState.AddModelError(nameof(RetailStore.Name), "A store with this name already exists in the selected city.");
        if (!ModelState.IsValid)
        {
            ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name", store.CityId);
            return View(store);
        }

        _db.Update(store);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the delete confirmation page.</summary>
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _db.RetailStores.Include(r => r.City).FirstOrDefaultAsync(r => r.Id == id);
        if (s == null) return NotFound();
        return View(s);
    }

    /// <summary>Deletes the store (only when no employee or order refers to it).</summary>
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var s = await _db.RetailStores.FindAsync(id);

        // VALIDATION (added): a store with employees or orders cannot be deleted.
        if (s != null)
        {
            bool inUse = await _db.Employees.AnyAsync(e => e.RetailStoreId == id)
                || await _db.Orders.AnyAsync(o => o.RetailStoreId == id);
            if (inUse)
            {
                TempData["Error"] = "This store has employees or orders and cannot be deleted. Untick 'Is Active' instead.";
                return RedirectToAction(nameof(Index));
            }
        }

        if (s != null) { _db.RetailStores.Remove(s); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
