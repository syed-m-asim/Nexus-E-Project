namespace NexusCommunication.Models;

/// <summary>Serial counters used to build order ids, account ids, bill numbers etc.</summary>
public class SequenceCounter
{
    /// <summary>Name of the counter (for example the order-id prefix).</summary>
    public string Key { get; set; } = "";
    /// <summary>Last serial number that was handed out.</summary>
    public long LastValue { get; set; }
}
