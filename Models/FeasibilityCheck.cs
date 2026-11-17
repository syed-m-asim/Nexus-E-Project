using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>Result of one feasibility check (distance, server availability) done by the technical team for an order.</summary>
public class FeasibilityCheck
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Order that was checked.</summary>
    public int OrderId { get; set; }
    /// <summary>Landline or internet feasibility.</summary>
    public FeasibilityType CheckType { get; set; }
    /// <summary>Distance used for the feasibility decision, in kilometres.</summary>
    public double DistanceKm { get; set; }
    /// <summary>Whether a server / port is available for the connection.</summary>
    public bool ServerAvailable { get; set; }
    /// <summary>Final decision of the technical employee.</summary>
    public bool IsFeasible { get; set; }
    /// <summary>Free-text remarks (optional).</summary>
    [MaxLength(500)] public string? Remarks { get; set; }
    /// <summary>Technical employee who did the check.</summary>
    public int CheckedByEmployeeId { get; set; }
    /// <summary>Date and time of the check.</summary>
    public DateTime CheckedOn { get; set; } = DateTime.UtcNow;
}
