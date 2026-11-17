using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>A retail outlet where customers can enquire, place orders and pay bills. Maintained by the Admin.</summary>
public class RetailStore : IEntity, IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Store name is required.")]
    [MinLength(2, ErrorMessage = "Store name must be at least 2 characters long.")]
    [MaxLength(150, ErrorMessage = "Store name cannot be longer than 150 characters.")]
    public string Name { get; set; } = "";

    [MaxLength(300, ErrorMessage = "Address cannot be longer than 300 characters.")]
    public string? Address { get; set; }

    /// <summary>City in which the store is located. A real city must be selected (id 0 means "nothing selected").</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Please select a city.")]
    public int CityId { get; set; }
    public City? City { get; set; }

    [RegularExpression(@"^\+?[0-9][0-9\s\-()]{6,19}$",
        ErrorMessage = "Enter a valid phone number (7 to 20 digits; spaces, - and ( ) are allowed; may start with +).")]
    [MaxLength(30)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Address and phone stay optional in the database, but the form must always fill them.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Address)) yield return new ValidationResult("Address is required.", new[] { nameof(Address) });
        if (string.IsNullOrWhiteSpace(Phone)) yield return new ValidationResult("Phone number is required.", new[] { nameof(Phone) });
    }
}
