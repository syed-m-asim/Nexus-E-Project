using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Helpers;

/// <summary>
/// Thread-safe serial number generation using the SequenceCounters table.
/// Avoids the race condition of "last + 1" when two users save at the same second.
/// </summary>
public static class SequenceHelper
{
    public static async Task<long> NextAsync(ApplicationDbContext db, string key)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var row = await db.SequenceCounters.FirstOrDefaultAsync(s => s.Key == key);
            if (row == null)
            {
                row = new SequenceCounter { Key = key, LastValue = 0 };
                db.SequenceCounters.Add(row);
                await db.SaveChangesAsync();
            }

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE SequenceCounters SET LastValue = LastValue + 1 WHERE [Key] = {0}", key);

            var next = await db.SequenceCounters
                .Where(s => s.Key == key)
                .Select(s => s.LastValue)
                .FirstAsync();

            await tx.CommitAsync();
            return next;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public static async Task<string> NextOrderNumberAsync(ApplicationDbContext db, string letter)
    {
        var n = await NextAsync(db, "Order:" + letter);
        return $"{letter}{n:D10}";
    }

    public static async Task<string> NextAccountIdAsync(ApplicationDbContext db, string typeLetter, string cityCode)
    {
        var key = $"Account:{typeLetter}{cityCode}";
        var n = await NextAsync(db, key);
        return $"{typeLetter}{cityCode}{n:D12}";
    }

    public static async Task<string> NextBillNumberAsync(ApplicationDbContext db)
    {
        var n = await NextAsync(db, "Bill");
        return $"BILL{n:D8}";
    }

    public static async Task<string> NextReceiptNumberAsync(ApplicationDbContext db)
    {
        var n = await NextAsync(db, "Receipt");
        return $"RCPT{n:D8}";
    }

    public static async Task<string> NextCustomerCodeAsync(ApplicationDbContext db)
    {
        var n = await NextAsync(db, "Customer");
        return $"{n:D10}";
    }

    public static async Task<string> NextPoNumberAsync(ApplicationDbContext db)
    {
        var n = await NextAsync(db, "PO");
        return $"PO{n:D8}";
    }
}
