using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>One line on a bill (plan charge, deposit, usage, replacement, discount or tax).</summary>
public class BillLineItem
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Bill this line belongs to.</summary>
    public int BillId { get; set; }
    /// <summary>Kind of charge.</summary>
    public BillLineType LineType { get; set; }
    /// <summary>Text printed on the bill for this line.</summary>
    [MaxLength(300)] public string Description { get; set; } = "";
    /// <summary>Units charged (minutes, items, 1 for flat charges).</summary>
    public decimal Quantity { get; set; } = 1;
    /// <summary>Price of one unit.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>Negative for discount lines.</summary>
    public decimal Amount { get; set; }
}
