using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;

namespace NexusCommunication.Helpers;

/// <summary>
/// Retail employees may only work with the data of their own retail store.
/// The employee is found through the login e-mail (the Employee record must have the same e-mail).
/// </summary>
public static class StoreScope
{
    /// <summary>Returns the store id of a retail employee (0 = no store linked, so nothing is visible), or null for all other roles (no restriction).</summary>
    public static async Task<int?> GetRetailStoreIdAsync(ApplicationDbContext db, ClaimsPrincipal user)
    {
        if (!user.IsInRole("RetailEmployee")) return null;
        var email = user.Identity?.Name;
        if (string.IsNullOrEmpty(email)) return 0;
        var storeId = await db.Employees
            .Where(e => e.Email == email && e.IsActive)
            .Select(e => e.RetailStoreId).FirstOrDefaultAsync();
        return storeId ?? 0;
    }
}
