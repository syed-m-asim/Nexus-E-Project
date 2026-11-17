namespace NexusCommunication.Models;

/// <summary>Helper methods for the <see cref="ConnectionType"/> enum.</summary>
public static class ConnectionTypeExtensions
{
    /// <summary>D = dial-up, B = broadband, T = telephone only.</summary>
    public static char Letter(this ConnectionType t) => t switch
    {
        ConnectionType.DialUp => 'D',
        ConnectionType.Broadband => 'B',
        _ => 'T'
    };
}
