using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Helpers;
using NexusCommunication.Models;
using NexusCommunication.ViewModels;

namespace NexusCommunication.Controllers;

/// <summary>
/// Login, logout and registration of users (employees and customers) using ASP.NET Core Identity.
/// The e-mail address is used as the user name.
/// </summary>
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _db;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
    }

    /// <summary>Shows the login form.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>Checks the credentials. On success the user is redirected to the page he came from (local URLs only).</summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Model validation (required, e-mail format) is defined on LoginViewModel.
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Url.IsLocalUrl prevents "open redirect" attacks.
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError("", "Invalid login attempt.");
        return View(model);
    }

    /// <summary>Shows the registration form (with the list of cities).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        await LoadCitiesAsync(null);
        return View(new RegisterViewModel());
    }

    /// <summary>
    /// Creates a new customer account: the login (Identity user with the Customer role) and the customer record.
    /// - If a customer record with the same e-mail already exists (created at a retail outlet), no second record is made;
    ///   the login is linked to it, but only when the contact number matches (so nobody can take over another person's data).
    /// - Otherwise a new customer record with a unique 10-digit customer code is created.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Clean the text fields first so that "  " is treated as empty and e-mails compare correctly.
        model.FullName = (model.FullName ?? "").Trim();
        model.Email = (model.Email ?? "").Trim();
        model.Phone = (model.Phone ?? "").Trim();
        model.Address = (model.Address ?? "").Trim();

        // The customer table allows an e-mail of 150 characters only (the login allows 256).
        if (model.Email.Length > 150)
            ModelState.AddModelError(nameof(model.Email), "E-mail cannot be longer than 150 characters.");

        // The selected city must exist.
        if (model.CityId.HasValue && !await _db.Cities.AnyAsync(c => c.Id == model.CityId.Value))
            ModelState.AddModelError(nameof(model.CityId), "The selected city does not exist.");

        if (!ModelState.IsValid)
        {
            await LoadCitiesAsync(model.CityId);
            return View(model);
        }

        // This e-mail must not already have a login.
        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), "This e-mail is already registered. Please log in instead.");
            await LoadCitiesAsync(model.CityId);
            return View(model);
        }

        // Does a customer record with this e-mail already exist (created at a retail outlet)?
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Email == model.Email);
        if (existing != null)
        {
            if (!existing.IsActive)
            {
                ModelState.AddModelError("", "The customer record for this e-mail is inactive. Please contact the retail outlet.");
                await LoadCitiesAsync(model.CityId);
                return View(model);
            }
            // Identity check: the contact number given here must be the one on the customer record.
            if (!SamePhone(existing.Phone, model.Phone))
            {
                ModelState.AddModelError(nameof(model.Phone),
                    "A customer with this e-mail already exists, but the contact number does not match. Please contact the retail outlet.");
                await LoadCitiesAsync(model.CityId);
                return View(model);
            }
        }

        // 1) Create the login.
        var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FullName = model.FullName };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            // Identity errors (weak password ...) are shown on the form.
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            await LoadCitiesAsync(model.CityId);
            return View(model);
        }

        // Public registration always creates a Customer. Staff logins are created by the Admin (Employees page).
        if (await _roleManager.RoleExistsAsync("Customer"))
            await _userManager.AddToRoleAsync(user, "Customer");

        // 2) Create the customer record (only when there is none yet) with its 10-digit customer code.
        string customerCode;
        try
        {
            if (existing == null)
            {
                var customer = new Customer
                {
                    CustomerCode = await SequenceHelper.NextCustomerCodeAsync(_db),
                    Name = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    Address = model.Address,
                    CityId = model.CityId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();
                customerCode = customer.CustomerCode;
            }
            else
            {
                customerCode = existing.CustomerCode;
            }
        }
        catch
        {
            // Do not leave a login without a customer record behind: remove it so the person can try again.
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError("", "Registration could not be completed. Please try again.");
            await LoadCitiesAsync(model.CityId);
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = existing == null
            ? $"Welcome! Your account is ready. Your customer code is {customerCode}."
            : $"Welcome back! Your login is now linked to your customer record ({customerCode}).";
        return RedirectToAction("Index", "MyPortal");
    }

    /// <summary>Signs the current user out (POST only, protected by the anti-forgery token).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Shows the change password form (any logged-in user).</summary>
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    /// <summary>
    /// Changes the password of the logged-in user after checking the current password.
    /// The user stays signed in (the security stamp is refreshed).
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            // The login no longer exists (for example it was deleted): sign out and go to the login page.
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (model.NewPassword == model.CurrentPassword)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "The new password must be different from the current password.");
            return View(model);
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                if (error.Code == "PasswordMismatch")
                    ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is incorrect.");
                else
                    ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        // Keep the user signed in with the new security stamp.
        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Your password has been changed.";
        return User.IsInRole("Customer")
            ? RedirectToAction("Index", "MyPortal")
            : RedirectToAction("Index", "Home");
    }

    /// <summary>Shown when a logged-in user opens a page that his role is not allowed to use.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>Fills the city drop-down of the registration form (ViewBag.CityId).</summary>
    private async Task LoadCitiesAsync(int? selectedCityId)
    {
        var cities = await _db.Cities.OrderBy(c => c.Name).ToListAsync();
        ViewBag.CityId = new SelectList(cities, "Id", "Name", selectedCityId);
    }

    /// <summary>
    /// Compares two phone numbers ignoring spaces, dashes, brackets and the leading + / country code:
    /// only the digits are compared (last 10 digits when both numbers have at least 10 digits).
    /// </summary>
    private static bool SamePhone(string? a, string? b)
    {
        var da = new string((a ?? "").Where(char.IsDigit).ToArray());
        var db = new string((b ?? "").Where(char.IsDigit).ToArray());
        if (da.Length == 0 || db.Length == 0) return false;
        if (da.Length >= 10 && db.Length >= 10)
            return da[^10..] == db[^10..];
        return da == db;
    }
}