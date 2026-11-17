namespace NexusCommunication.Models;

/// <summary>Kind of charge printed on a bill line.</summary>
public enum BillLineType
{
    /// <summary>Refundable security deposit (first bill only).</summary>
    SecurityDeposit = 1,
    /// <summary>Plan rental / plan charge for the period.</summary>
    PlanCharge = 2,
    /// <summary>Monthly rental of modem / router.</summary>
    EquipmentRental = 3,
    /// <summary>Call usage charge (local, STD, mobile messaging).</summary>
    UsageCharge = 4,
    /// <summary>Charge for replacing damaged equipment.</summary>
    ReplacementCharge = 5,
    /// <summary>Bulk / corporate discount (negative amount).</summary>
    Discount = 6,
    /// <summary>Service tax on the bill.</summary>
    Tax = 7
}
