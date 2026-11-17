namespace NexusCommunication.Models;

/// <summary>Status of a purchase order placed with a vendor.</summary>
public enum PurchaseOrderStatus
{
    /// <summary>Order placed, goods not received yet.</summary>
    Ordered = 1,
    /// <summary>Goods received and stock updated.</summary>
    Received = 2,
    /// <summary>Order cancelled.</summary>
    Cancelled = 3
}
