using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Plans: Admin CRUD; all staff and customers can browse (read-only).
/// </summary>
[Authorize]
public class PlansController : Controller
{
    private readonly ApplicationDbContext _db;
    public PlansController(ApplicationDbContext db) => _db = db;

    [Authorize(Roles = "Admin,RetailEmployee,Technical,Accounts,Customer")]
    public async Task<IActionResult> Index()
    {
        var q = _db.Plans.AsQueryable();
        if (!User.IsInRole("Admin"))
            q = q.Where(p => p.IsActive);
        return View(await q.OrderBy(p => p.ConnectionType).ThenBy(p => p.Name).ToListAsync());
    }

    [Authorize(Roles = "Admin,RetailEmployee,Technical,Accounts,Customer")]
    public async Task<IActionResult> Details(int id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null) return NotFound();
        if (!plan.IsActive && !User.IsInRole("Admin")) return NotFound();
        return View(plan);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View(new Plan());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Plan plan)
    {
        if (!ModelState.IsValid) return View(plan);
        if (await _db.Plans.AnyAsync(p => p.Name == plan.Name && p.ConnectionType == plan.ConnectionType))
            ModelState.AddModelError(nameof(Plan.Name), "A plan with this name already exists for this connection type.");
        if (!ModelState.IsValid) return View(plan);
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null) return NotFound();
        return View(plan);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, Plan plan)
    {
        if (id != plan.Id) return NotFound();
        if (!ModelState.IsValid) return View(plan);
        if (await _db.Plans.AnyAsync(p => p.Name == plan.Name && p.ConnectionType == plan.ConnectionType && p.Id != plan.Id))
            ModelState.AddModelError(nameof(Plan.Name), "A plan with this name already exists for this connection type.");
        if (!ModelState.IsValid) return View(plan);
        _db.Update(plan);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null) return NotFound();
        return View(plan);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan != null)
        {
            bool inUse = await _db.Orders.AnyAsync(o => o.PlanId == id || o.LandlinePlanId == id)
                || await _db.Connections.AnyAsync(c => c.PlanId == id);
            if (inUse)
            {
                TempData["Error"] = "This plan is used by orders or connections and cannot be deleted. Untick 'Is Active' instead.";
                return RedirectToAction(nameof(Index));
            }
            _db.Plans.Remove(plan);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
