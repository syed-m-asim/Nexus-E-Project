using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace NexusCommunication.Models;

/// <summary>
/// Login user of the application (ASP.NET Core Identity user + the person's full name).
/// UserName / Email stay the e-mail address (used to log in); FullName is the readable name.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Full name of the person (letters and spaces only). Stored in the AspNetUsers table.</summary>
    [MaxLength(150)]
    public string? FullName { get; set; }
}
