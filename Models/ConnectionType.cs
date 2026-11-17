namespace NexusCommunication.Models;

/// <summary>Type of connection. Letter is used in order ids and account ids (D / B / T).</summary>
public enum ConnectionType
{
    /// <summary>Dial-up internet (needs a landline of the same vendor).</summary>
    DialUp = 1,
    /// <summary>Broadband internet.</summary>
    Broadband = 2,
    /// <summary>Telephone (landline) connection only.</summary>
    TelephoneOnly = 3
}
