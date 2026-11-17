using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>A plan of the Dial-up, Broadband or Telephone service. Maintained by the Admin.</summary>
public class Plan : IEntity, IValidatableObject
{
    public int Id { get; set; }

    [EnumDataType(typeof(ConnectionType), ErrorMessage = "Select a valid connection type.")]
    [Display(Name = "Connection Type")]
    public ConnectionType ConnectionType { get; set; }

    /// <summary>E.g. "Hourly Basis", "Unlimited 64Kbps", "Local", "STD".</summary>
    [Required(ErrorMessage = "Plan group is required.")]
    [MaxLength(100, ErrorMessage = "Plan group cannot be longer than 100 characters.")]
    [Display(Name = "Plan Group")]
    public string PlanGroup { get; set; } = "";

    [Required(ErrorMessage = "Plan name is required.")]
    [MinLength(2, ErrorMessage = "Plan name must be at least 2 characters long.")]
    [MaxLength(150, ErrorMessage = "Plan name cannot be longer than 150 characters.")]
    public string Name { get; set; } = "";

    [MaxLength(500, ErrorMessage = "Description cannot be longer than 500 characters.")]
    public string? Description { get; set; }

    /// <summary>Plan charge / rental for one validity period.</summary>
    [Range(0.01, 10000000.0, ErrorMessage = "Price must be greater than zero (maximum 10,000,000).")]
    public decimal Price { get; set; }

    /// <summary>Number of days the plan stays valid (at least 1 day, at most 10 years).</summary>
    [Range(1, 3650, ErrorMessage = "Validity must be between 1 and 3650 days.")]
    [Display(Name = "Validity (days)")]
    public int ValidityDays { get; set; }

    /// <summary>Hours included (hourly plans only).</summary>
    [Range(1, 100000, ErrorMessage = "Hours must be between 1 and 100,000.")]
    public int? Hours { get; set; }

    /// <summary>Speed (unlimited plans only).</summary>
    [Range(1, 1000000, ErrorMessage = "Speed must be between 1 and 1,000,000 Kbps.")]
    [Display(Name = "Speed (Kbps)")]
    public int? SpeedKbps { get; set; }

    // Landline call charges, per minute
    [Range(0.0, 1000.0, ErrorMessage = "Local call rate must be between 0 and 1000 per minute.")]
    [Display(Name = "Local Call Rate (per min)")]
    public decimal? LocalCallRate { get; set; }

    [Range(0.0, 1000.0, ErrorMessage = "STD call rate must be between 0 and 1000 per minute.")]
    [Display(Name = "STD Call Rate (per min)")]
    public decimal? StdCallRate { get; set; }

    [Range(0.0, 1000.0, ErrorMessage = "Mobile messaging rate must be between 0 and 1000 per minute.")]
    [Display(Name = "Mobile Messaging Rate (per min)")]
    public decimal? MobileMessageRate { get; set; }

    /// <summary>Inactive plans cannot be selected for new orders.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Rules that depend on the connection type.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConnectionType == ConnectionType.DialUp || ConnectionType == ConnectionType.Broadband)
        {
            // Internet plans are either hourly (Hours) or unlimited (SpeedKbps) - exactly one of the two.
            if (!Hours.HasValue && !SpeedKbps.HasValue)
                yield return new ValidationResult("Enter the hours (hourly plan) or the speed (unlimited plan).", new[] { nameof(Hours), nameof(SpeedKbps) });
            if (Hours.HasValue && SpeedKbps.HasValue)
                yield return new ValidationResult("A plan is either hourly or unlimited - fill only one of Hours / Speed.", new[] { nameof(Hours), nameof(SpeedKbps) });
        }
        else if (ConnectionType == ConnectionType.TelephoneOnly && !LocalCallRate.HasValue)
        {
            // Landline plans need at least the local call rate.
            yield return new ValidationResult("Local call rate is required for a telephone plan.", new[] { nameof(LocalCallRate) });
        }
    }
}
