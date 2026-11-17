namespace NexusCommunication.Models;

/// <summary>Workflow status of a connection order.</summary>
public enum OrderStatus
{
    /// <summary>Order received, nothing checked yet.</summary>
    Placed = 1,
    /// <summary>Feasibility checks have started.</summary>
    FeasibilityInProgress = 2,
    /// <summary>Area is feasible; connection can be created.</summary>
    Feasible = 3,
    /// <summary>Area is not feasible; no connection will be given.</summary>
    NotFeasible = 4,
    /// <summary>Some of the requested connections are created.</summary>
    PartiallyProvided = 5,
    /// <summary>Connection(s) have been provided.</summary>
    ConnectionProvided = 6,
    /// <summary>Order was cancelled.</summary>
    Cancelled = 7
}
