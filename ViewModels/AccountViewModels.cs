using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.ViewModels;

/// <summary>Data entered on the login form.</summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "E-mail is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [StringLength(256, ErrorMessage = "E-mail cannot be longer than 256 characters.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>Keep the user logged in after the browser is closed.</summary>
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>Page the user wanted to open before he was sent to the login page.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// Data entered on the public registration form. Public registration always creates a Customer account
/// (login + customer record); staff accounts are created by the Admin on the Employees page.
/// </summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
    [RegularExpression(@"^[A-Za-z]+( [A-Za-z]+)*$", ErrorMessage = "Full name can contain only letters and single spaces (no digits or symbols).")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "E-mail is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [StringLength(256, ErrorMessage = "E-mail cannot be longer than 256 characters.")]
    public string Email { get; set; } = "";

    /// <summary>
    /// Contact number. Same rule as Customer.Phone. It is also used to confirm the identity of a customer
    /// who already has a record created at a retail outlet with the same e-mail.
    /// </summary>
    [Required(ErrorMessage = "Contact number is required.")]
    [RegularExpression(@"^\+?[0-9][0-9\s\-()]{6,19}$",
        ErrorMessage = "Enter a valid phone number (7 to 20 digits; spaces, - and ( ) are allowed; may start with +).")]
    [StringLength(30, ErrorMessage = "Contact number cannot be longer than 30 characters.")]
    [Display(Name = "Contact Number")]
    public string Phone { get; set; } = "";

    /// <summary>Installation / postal address of the customer (same rule as Customer.Address).</summary>
    [Required(ErrorMessage = "Address is required.")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Address must be between 5 and 300 characters.")]
    public string Address { get; set; } = "";

    /// <summary>City of the customer (taken from the cities maintained by the Admin).</summary>
    [Required(ErrorMessage = "Please select your city.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select your city.")]
    [Display(Name = "City")]
    public int? CityId { get; set; }

    /// <summary>Same rule as the Identity options in Program.cs: at least 6 characters and at least one digit.</summary>
    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters.")]
    [RegularExpression(@"^(?=.*\d).+$", ErrorMessage = "Password must contain at least one digit.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = "";
}

/// <summary>Data entered on the Change Password form (any logged-in user, staff or customer).</summary>
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = "";

    /// <summary>Same rule as the Identity options in Program.cs: at least 6 characters and at least one digit.</summary>
    [Required(ErrorMessage = "New password is required.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "New password must be between 6 and 100 characters.")]
    [RegularExpression(@"^(?=.*\d).+$", ErrorMessage = "New password must contain at least one digit.")]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Please confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "New password and confirmation password do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmNewPassword { get; set; } = "";
}