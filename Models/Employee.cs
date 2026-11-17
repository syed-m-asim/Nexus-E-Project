using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexusCommunication.Models;

/// <summary>
/// Admin, retail outlet staff, technical staff and accounts staff. Maintained by the Admin only.
/// Cross-field rules (role vs. retail store) are checked in <see cref="Validate"/>.
/// </summary>
public class Employee : IEntity, IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Employee name is required.")]
    [MinLength(2, ErrorMessage = "Employee name must be at least 2 characters long.")]
    [RegularExpression(@"^[A-Za-z]+( [A-Za-z]+)*$", ErrorMessage = "Employee name can contain only letters and single spaces (no digits or symbols).")]
    [MaxLength(150, ErrorMessage = "Employee name cannot be longer than 150 characters.")]
    public string Name { get; set; } = "";

    [MaxLength(100, ErrorMessage = "Designation cannot be longer than 100 characters.")]
    public string? Designation { get; set; }

    [RegularExpression(@"^\+?[0-9][0-9\s\-()]{6,19}$",
        ErrorMessage = "Enter a valid phone number (7 to 20 digits; spaces, - and ( ) are allowed; may start with +).")]
    [MaxLength(30)]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [MaxLength(150, ErrorMessage = "E-mail cannot be longer than 150 characters.")]
    public string? Email { get; set; }

    /// <summary>Monthly salary. Must not be negative.</summary>
    [Range(1.0, 10000000.0, ErrorMessage = "Salary must be between 1 and 10,000,000.")]
    public decimal Salary { get; set; }

    public DateTime JoinDate { get; set; } = DateTime.UtcNow;

    /// <summary>Role of the employee (Admin, RetailEmployee, Technical or Accounts).</summary>
    [EnumDataType(typeof(UserRole), ErrorMessage = "Select a valid role.")]
    public UserRole Role { get; set; }

    /// <summary>Required for retail employees; they only see data of their own store.</summary>
    public int? RetailStoreId { get; set; }
    public RetailStore? RetailStore { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Initial password of the employee's login (used on Create only; NOT stored in the Employees table).
    /// The login user name is the employee's e-mail and the login role is the employee's role.
    /// </summary>
    [NotMapped]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Password cannot be longer than 100 characters.")]
    public string? Password { get; set; }

    /// <summary>Cross-field validation that cannot be expressed with a single attribute.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // These columns stay optional in the database, but the form must always fill them.
        if (string.IsNullOrWhiteSpace(Designation)) yield return new ValidationResult("Designation is required.", new[] { nameof(Designation) });
        if (string.IsNullOrWhiteSpace(Phone)) yield return new ValidationResult("Phone number is required.", new[] { nameof(Phone) });
        if (string.IsNullOrWhiteSpace(Email)) yield return new ValidationResult("E-mail is required (it is the login name).", new[] { nameof(Email) });

        // Customers are not employees; the Customer role is reserved for portal users.
        if (Role == UserRole.Customer)
            yield return new ValidationResult("An employee cannot have the Customer role.", new[] { nameof(Role) });

        // A retail employee must belong to a retail outlet.
        if (Role == UserRole.RetailEmployee && !RetailStoreId.HasValue)
            yield return new ValidationResult("A retail employee must be assigned to a retail store.", new[] { nameof(RetailStoreId) });

        // A joining date in the future makes no sense for an existing employee.
        if (JoinDate.Date > DateTime.UtcNow.Date.AddDays(1))
            yield return new ValidationResult("Join date cannot be in the future.", new[] { nameof(JoinDate) });
    }
}
