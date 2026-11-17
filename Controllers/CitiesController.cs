using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Maintenance of the cities of the territory (Admin only).
/// Every city has a unique 3-digit code which becomes part of the 16-character account id.
/// </summary>
[Authorize(Roles = "Admin")]
public class CitiesController : Controller
{
    private readonly ApplicationDbContext _db;
    public CitiesController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all cities.</summary>
    public async Task<IActionResult> Index() => View(await _db.Cities.ToListAsync());

    /// <summary>Shows the empty "add city" form.</summary>
    public IActionResult Create() => View(new City());

    /// <summary>Saves a new city after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(City city)
    {
        if (!ModelState.IsValid) return View(city);

        // VALIDATION (added): the city code and the city name must be unique.
        // The database has a unique index on the code, so without this check a duplicate would end in an exception page.
        if (await _db.Cities.AnyAsync(c => c.Code == city.Code))
            ModelState.AddModelError(nameof(City.Code), "A city with this code already exists.");
        if (await _db.Cities.AnyAsync(c => c.Name == city.Name))
            ModelState.AddModelError(nameof(City.Name), "A city with this name already exists.");
        if (!ModelState.IsValid) return View(city);

        _db.Cities.Add(city);
        await _db.SaveChangesAsync();
        TempData["Success"] = "City added.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the edit form of a city.</summary>
    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Cities.FindAsync(id);
        if (c == null) return NotFound();
        return View(c);
    }

    /// <summary>Saves the changes of a city after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, City city)
    {
        if (id != city.Id) return NotFound();
        if (!ModelState.IsValid) return View(city);

        // VALIDATION (added): code / name must stay unique (the city being edited is excluded from the comparison).
        if (await _db.Cities.AnyAsync(c => c.Code == city.Code && c.Id != city.Id))
            ModelState.AddModelError(nameof(City.Code), "A city with this code already exists.");
        if (await _db.Cities.AnyAsync(c => c.Name == city.Name && c.Id != city.Id))
            ModelState.AddModelError(nameof(City.Name), "A city with this name already exists.");
        if (!ModelState.IsValid) return View(city);

        _db.Update(city);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the delete confirmation page.</summary>
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Cities.FindAsync(id);
        if (c == null) return NotFound();
        return View(c);
    }

    /// <summary>Deletes the city (only when no other record uses it).</summary>
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var c = await _db.Cities.FindAsync(id);

        // VALIDATION (added): a city that is already used cannot be deleted (foreign keys are set to Restrict).
        if (c != null)
        {
            bool inUse = await _db.RetailStores.AnyAsync(r => r.CityId == id)
                || await _db.Customers.AnyAsync(x => x.CityId == id)
                || await _db.Orders.AnyAsync(o => o.CityId == id)
                || await _db.Connections.AnyAsync(x => x.CityId == id)
                || await _db.CustomerDocuments.AnyAsync(d => d.CityId == id);
            if (inUse)
            {
                TempData["Error"] = "This city is used by stores, customers, orders, connections or documents and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }
        }

        if (c != null) { _db.Cities.Remove(c); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
