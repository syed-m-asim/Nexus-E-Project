using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;

namespace NexusCommunication.Helpers;

/// <summary>Reads the bulk / corporate discount percentage from the BulkDiscountTiers table (10-14 = 25%, 15-24 = 50%, 25-49 = 75%, 50+ = 100%).</summary>
public static class BulkDiscount
{
    public static async Task<decimal> GetPercentAsync(ApplicationDbContext db, int numberOfConnections)
    {
        var tier = await db.BulkDiscountTiers
            .Where(t => t.MinConnections <= numberOfConnections && (t.MaxConnections == null || t.MaxConnections >= numberOfConnections))
            .OrderByDescending(t => t.MinConnections)
            .FirstOrDefaultAsync();
        return tier?.DiscountPercent ?? 0;
    }
}
