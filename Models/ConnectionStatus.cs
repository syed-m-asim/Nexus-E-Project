namespace NexusCommunication.Models;

/// <summary>Status of a provided connection.</summary>
public enum ConnectionStatus
{
    /// <summary>Connection is working.</summary>
    Active = 1,
    /// <summary>Connection is suspended and can be re-activated.</summary>
    TemporarilyInactive = 2,
    /// <summary>Connection is closed for good.</summary>
    PermanentlyInactive = 3
}
