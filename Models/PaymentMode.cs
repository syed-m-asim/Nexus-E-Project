namespace NexusCommunication.Models;

/// <summary>How a payment was made.</summary>
public enum PaymentMode
{
    /// <summary>Cash at the counter.</summary>
    Cash = 1,
    /// <summary>Debit / credit card.</summary>
    Card = 2,
    /// <summary>Cheque (reference number is required).</summary>
    Cheque = 3,
    /// <summary>Online transfer (reference number is required).</summary>
    Online = 4
}
