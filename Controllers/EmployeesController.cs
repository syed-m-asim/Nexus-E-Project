using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Employee records (Admin, retail, technical and accounts staff). Only the Admin can maintain them.
/// </summary>
[Authorize(Roles = "Admin")]
public class EmployeesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public EmployeesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    /// <summary>Lists all employees with their retail store.</summary>
    public async Task<IActionResult> Index() =>
        View(await _db.Employees.Include(e => e.RetailStore).ToListAsync());

    /// <summary>Shows the details of one employee.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var e = await _db.Employees.Include(x => x.RetailStore).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        return View(e);
    }

    /// <summary>Shows the empty "add employee" form (with the retail store drop-down).</summary>
    public async Task<IActionResult> Create()
    {
        ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name");
        return View(new Employee());
    }

    /// <summary>Saves a new employee after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee e)
    {
        // Attribute rules and the role / store rule are defined on the Employee model.
        if (!ModelState.IsValid)
        {
            ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name");
            return View(e);
        }

        // VALIDATION (added): the selected store must exist and the e-mail must be unique among employees.
        if (e.RetailStoreId.HasValue && !await _db.RetailStores.AnyAsync(s => s.Id == e.RetailStoreId.Value))
            ModelState.AddModelError(nameof(Employee.RetailStoreId), "The selected retail store does not exist.");
        if (!string.IsNullOrWhiteSpace(e.Email) && await _db.Employees.AnyAsync(x => x.Email == e.Email))
            ModelState.AddModelError(nameof(Employee.Email), "Another employee already uses this e-mail address.");
        // Every employee gets a login (e-mail + password) so he can use the application with his role.
        if (string.IsNullOrWhiteSpace(e.Email))
            ModelState.AddModelError(nameof(Employee.Email), "E-mail is required because it is the login name of the employee.");
        if (string.IsNullOrWhiteSpace(e.Password) || e.Password.Length < 6 || !e.Password.Any(char.IsDigit))
            ModelState.AddModelError(nameof(Employee.Password), "Password is required (at least 6 characters and one digit).");
        if (!string.IsNullOrWhiteSpace(e.Email) && await _userManager.FindByEmailAsync(e.Email) != null)
            ModelState.AddModelError(nameof(Employee.Email), "A login with this e-mail already exists.");
        if (!ModelState.IsValid)
        {
            ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name");
            return View(e);
        }

        // Create the login first; the employee record is saved only when the login could be created.
        var roleName = e.Role.ToString();
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new IdentityRole(roleName));
        var loginUser = new ApplicationUser { UserName = e.Email, Email = e.Email, EmailConfirmed = true, FullName = e.Name };
        var created = await _userManager.CreateAsync(loginUser, e.Password!);
        if (!created.Succeeded)
        {
            foreach (var err in created.Errors) ModelState.AddModelError(nameof(Employee.Password), err.Description);
            ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name");
            return View(e);
        }
        await _userManager.AddToRoleAsync(loginUser, roleName);

        _db.Employees.Add(e);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Employee added. Login: " + e.Email;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the edit form of an employee.</summary>
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound();
        ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name", e.RetailStoreId);
        return View(e);
    }

    /// <summary>Saves the changes of an employee after validating the input.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Employee e)
    {
        if (id != e.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name", e.RetailStoreId);
            return View(e);
        }

        if (e.RetailStoreId.HasValue && !await _db.RetailStores.AnyAsync(s => s.Id == e.RetailStoreId.Value))
            ModelState.AddModelError(nameof(Employee.RetailStoreId), "The selected retail store does not exist.");
        if (!string.IsNullOrWhiteSpace(e.Email) &&
            await _db.Employees.AnyAsync(x => x.Email == e.Email && x.Id != e.Id))
            ModelState.AddModelError(nameof(Employee.Email), "Another employee already uses this e-mail address.");
        if (!ModelState.IsValid)
        {
            ViewBag.RetailStoreId = new SelectList(await _db.RetailStores.ToListAsync(), "Id", "Name", e.RetailStoreId);
            return View(e);
        }

        // ★ Pehle DB se purana record lo taake purana email pata chale
        var existing = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (existing == null) return NotFound();

        var oldEmail = existing.Email?.Trim();
        var newEmail = e.Email?.Trim();

        _db.Update(e);
        await _db.SaveChangesAsync();

        // Identity sync
        if (!string.IsNullOrWhiteSpace(oldEmail))
        {
            var loginUser = await _userManager.FindByEmailAsync(oldEmail);
            if (loginUser != null)
            {
                // Email change?
                if (!string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(newEmail))
                {
                    if (await _userManager.FindByEmailAsync(newEmail) == null)
                    {
                        loginUser.Email = newEmail;
                        loginUser.UserName = newEmail;
                        loginUser.NormalizedEmail = _userManager.NormalizeEmail(newEmail);
                        loginUser.NormalizedUserName = _userManager.NormalizeName(newEmail);
                    }
                }

                loginUser.FullName = e.Name;
                await _userManager.UpdateAsync(loginUser);

                // Role sync
                var currentRoles = await _userManager.GetRolesAsync(loginUser);
                if (!currentRoles.Contains(e.Role.ToString()) || currentRoles.Count != 1)
                {
                    await _userManager.RemoveFromRolesAsync(loginUser, currentRoles);
                    if (!await _roleManager.RoleExistsAsync(e.Role.ToString()))
                        await _roleManager.CreateAsync(new IdentityRole(e.Role.ToString()));
                    await _userManager.AddToRoleAsync(loginUser, e.Role.ToString());
                }

                await _userManager.SetLockoutEnabledAsync(loginUser, true);
                await _userManager.SetLockoutEndDateAsync(loginUser, e.IsActive ? null : DateTimeOffset.MaxValue);
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
