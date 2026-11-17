namespace NexusCommunication.Models;

/// <summary>Landline feasibility and internet feasibility are checked separately.</summary>
public enum FeasibilityType
{
    /// <summary>Feasibility of laying / using a telephone line.</summary>
    Landline = 1,
    /// <summary>Feasibility of the internet service (distance, server availability).</summary>
    Internet = 2
}
