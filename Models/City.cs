using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>A city in the territory. Its 3-digit code is part of the 16-char account id.</summary>
public class City : IEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "City name is required.")]
    [MinLength(2, ErrorMessage = "City name must be at least 2 characters long.")]
    [RegularExpression(@"^[A-Za-z]+( [A-Za-z]+)*$", ErrorMessage = "City name can contain only letters and single spaces (no digits or symbols).")]
    [MaxLength(100, ErrorMessage = "City name cannot be longer than 100 characters.")]
    [Display(Name = "City Name")]
    public string Name { get; set; } = "";

    /// <summary>Three-digit numeric city code used inside the account id (for example 001).</summary>
    [Required(ErrorMessage = "City code is required.")]
    [RegularExpression(@"^\d{3}$", ErrorMessage = "Code must be exactly 3 digits.")]
    [MaxLength(3)]
    [Display(Name = "City Code")]
    public string Code { get; set; } = "";
}
