using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;

namespace NexusCommunication.Helpers;

/// <summary>
/// Resolves the logged-in user's Employee.Id from the Identity e-mail.
/// Returns 0 when the user is a pure Customer or when nothing matches.
/// </summary>
public static class CurrentEmployee
{
    public static async Task<int> GetIdAsync(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var email = user.Identity?.Name;
        if (string.IsNullOrEmpty(email)) return 0;
        return await db.Employees
            .Where(e => e.Email == email && e.IsActive)
            .Select(e => e.Id)
            .FirstOrDefaultAsync();
    }
}
