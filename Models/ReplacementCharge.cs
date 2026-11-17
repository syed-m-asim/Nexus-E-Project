using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>Charge for replacing spoiled equipment. Added to the next bill (BillId null = not billed yet).</summary>
public class ReplacementCharge
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Connection whose equipment was replaced.</summary>
    public int ConnectionId { get; set; }
    /// <summary>Navigation property to the connection.</summary>
    public Connection? Connection { get; set; }
    /// <summary>Equipment that was replaced.</summary>
    public int ProductId { get; set; }
    /// <summary>Navigation property to the product.</summary>
    public Product? Product { get; set; }
    /// <summary>Number of units replaced.</summary>
    public int Quantity { get; set; }
    /// <summary>Total amount charged (replacement charge x quantity).</summary>
    public decimal Amount { get; set; }
    /// <summary>Why the equipment had to be replaced (optional).</summary>
    [MaxLength(300)] public string? Reason { get; set; }
    /// <summary>Date the charge was recorded.</summary>
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    /// <summary>Bill on which the charge was billed (null until billed).</summary>
    public int? BillId { get; set; }
    /// <summary>Employee who recorded the charge.</summary>
    public int CreatedByEmployeeId { get; set; }
}
