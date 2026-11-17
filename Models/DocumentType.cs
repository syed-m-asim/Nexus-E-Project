namespace NexusCommunication.Models;

/// <summary>Type of a document filed in the customer's record.</summary>
public enum DocumentType
{
    /// <summary>Proof of address.</summary>
    AddressProof = 1,
    /// <summary>Signed application form.</summary>
    ApplicationForm = 2,
    /// <summary>Payment receipt.</summary>
    Receipt = 3,
    /// <summary>Any other document.</summary>
    Other = 4
}
