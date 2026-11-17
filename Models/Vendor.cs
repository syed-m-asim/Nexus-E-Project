using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>A product manufacturing vendor from whom modems, routers etc. are purchased. Maintained by the Admin.</summary>
public class Vendor : IEntity, IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vendor name is required.")]
    [RegularExpression(@"^[A-Za-z]+( [A-Za-z]+)*$", ErrorMessage = "Vendor name can contain only letters and single spaces (no digits or symbols).")]
    [MinLength(2, ErrorMessage = "Vendor name must be at least 2 characters long.")]
    [MaxLength(150, ErrorMessage = "Vendor name cannot be longer than 150 characters.")]
    public string Name { get; set; } = "";

    [RegularExpression(@"^[A-Za-z]+( [A-Za-z]+)*$", ErrorMessage = "Contact person can contain only letters and single spaces (no digits or symbols).")]
    [MaxLength(150, ErrorMessage = "Contact person cannot be longer than 150 characters.")]
    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [RegularExpression(@"^\+?[0-9][0-9\s\-()]{6,19}$",
        ErrorMessage = "Enter a valid phone number (7 to 20 digits; spaces, - and ( ) are allowed; may start with +).")]
    [MaxLength(30)]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [MaxLength(150, ErrorMessage = "E-mail cannot be longer than 150 characters.")]
    public string? Email { get; set; }

    [MaxLength(300, ErrorMessage = "Address cannot be longer than 300 characters.")]
    public string? Address { get; set; }

    /// <summary>These columns stay optional in the database, but the form must always fill them.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ContactPerson)) yield return new ValidationResult("Contact person is required.", new[] { nameof(ContactPerson) });
        if (string.IsNullOrWhiteSpace(Phone)) yield return new ValidationResult("Phone number is required.", new[] { nameof(Phone) });
        if (string.IsNullOrWhiteSpace(Email)) yield return new ValidationResult("E-mail is required.", new[] { nameof(Email) });
        if (string.IsNullOrWhiteSpace(Address)) yield return new ValidationResult("Address is required.", new[] { nameof(Address) });
    }
}
