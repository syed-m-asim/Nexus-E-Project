using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>Material (modem / router / handset) provided with a connection.</summary>
public class ConnectionEquipment
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Connection the equipment was provided with.</summary>
    public int ConnectionId { get; set; }
    /// <summary>Product (modem / router / handset) provided.</summary>
    public int ProductId { get; set; }
    /// <summary>Navigation property to the product.</summary>
    public Product? Product { get; set; }
    /// <summary>Serial number of the physical unit (optional).</summary>
    [MaxLength(100)] public string? SerialNumber { get; set; }
    /// <summary>Date the equipment was handed to the customer.</summary>
    public DateTime AssignedOn { get; set; } = DateTime.UtcNow;
    /// <summary>Date the equipment was returned (null while with the customer).</summary>
    public DateTime? ReturnedOn { get; set; }
}
