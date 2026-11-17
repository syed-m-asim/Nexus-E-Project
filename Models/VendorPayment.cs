using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>A payment made by the company to a vendor against a purchase order.</summary>
public class VendorPayment
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Purchase order the payment is for.</summary>
    public int PurchaseOrderId { get; set; }
    /// <summary>Amount paid to the vendor.</summary>
    public decimal Amount { get; set; }
    /// <summary>Date of the payment.</summary>
    public DateTime PaidOn { get; set; } = DateTime.UtcNow;
    /// <summary>Cash, card, cheque or online.</summary>
    public PaymentMode Mode { get; set; }
    /// <summary>Cheque number / transaction reference (optional).</summary>
    [MaxLength(100)] public string? Reference { get; set; }
}
