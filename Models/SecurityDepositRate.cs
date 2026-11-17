namespace NexusCommunication.Models;

/// <summary>Refundable security deposit for each connection type.</summary>
public class SecurityDepositRate
{
    /// <summary>Connection type the deposit applies to (also the primary key).</summary>
    public ConnectionType ConnectionType { get; set; }
    /// <summary>Refundable deposit amount for that connection type.</summary>
    public decimal Amount { get; set; }
}
