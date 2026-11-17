namespace NexusCommunication.Models;

/// <summary>Payment status of a bill.</summary>
public enum BillStatus
{
    /// <summary>Nothing has been paid yet.</summary>
    Unpaid = 1,
    /// <summary>Some amount is paid, balance remains.</summary>
    PartiallyPaid = 2,
    /// <summary>Bill is fully paid.</summary>
    Paid = 3
}
