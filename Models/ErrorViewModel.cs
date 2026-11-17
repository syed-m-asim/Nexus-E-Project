namespace NexusCommunication.Models;

/// <summary>View model used by the shared error page.</summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
