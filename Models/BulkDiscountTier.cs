using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>Bulk / corporate scheme: discount on advance and security deposit by number of connections.</summary>
public class BulkDiscountTier : IEntity
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Lowest number of connections of the tier (inclusive).</summary>
    public int MinConnections { get; set; }
    /// <summary>Null means "and above".</summary>
    public int? MaxConnections { get; set; }
    /// <summary>Discount in percent given on the advance and the security deposit for this tier.</summary>
    [Range(0, 100)] public decimal DiscountPercent { get; set; }
}
