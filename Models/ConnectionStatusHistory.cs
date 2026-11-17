using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>History record written every time a connection is activated, made temporarily inactive or permanently inactive.</summary>
public class ConnectionStatusHistory
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Connection whose status changed.</summary>
    public int ConnectionId { get; set; }
    /// <summary>Status before the change.</summary>
    public ConnectionStatus OldStatus { get; set; }
    /// <summary>Status after the change.</summary>
    public ConnectionStatus NewStatus { get; set; }
    /// <summary>Why the status was changed (optional).</summary>
    [MaxLength(300)] public string? Reason { get; set; }
    /// <summary>Date and time of the change.</summary>
    public DateTime ChangedOn { get; set; } = DateTime.UtcNow;
    /// <summary>Employee who made the change.</summary>
    public int? ChangedByEmployeeId { get; set; }
}
