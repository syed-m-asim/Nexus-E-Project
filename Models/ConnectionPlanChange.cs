using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>History record written every time the plan of a connection is changed.</summary>
public class ConnectionPlanChange
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Connection whose plan was changed.</summary>
    public int ConnectionId { get; set; }
    /// <summary>Navigation property to the connection.</summary>
    public Connection? Connection { get; set; }
    /// <summary>Plan before the change.</summary>
    public int OldPlanId { get; set; }
    /// <summary>Plan after the change.</summary>
    public int NewPlanId { get; set; }
    /// <summary>Why the plan was changed (optional).</summary>
    [MaxLength(300)] public string? Reason { get; set; }
    /// <summary>Date and time of the change.</summary>
    public DateTime ChangedOn { get; set; } = DateTime.UtcNow;
    /// <summary>Employee who made the change.</summary>
    public int ChangedByEmployeeId { get; set; }
}
