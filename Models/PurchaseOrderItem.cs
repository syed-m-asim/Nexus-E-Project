namespace NexusCommunication.Models;

/// <summary>One product line (product, quantity, unit cost) of a purchase order.</summary>
public class PurchaseOrderItem
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Purchase order the line belongs to.</summary>
    public int PurchaseOrderId { get; set; }
    /// <summary>Product that is ordered.</summary>
    public int ProductId { get; set; }
    /// <summary>Navigation property to the product.</summary>
    public Product? Product { get; set; }
    /// <summary>Number of units ordered.</summary>
    public int Quantity { get; set; }
    /// <summary>Cost of one unit.</summary>
    public decimal UnitCost { get; set; }
}
