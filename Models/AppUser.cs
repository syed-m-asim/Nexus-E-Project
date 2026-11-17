using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>Login credentials for employees and customers. Never returned by the API.</summary>
public class AppUser
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Login name.</summary>
    [Required, MaxLength(100)] public string Username { get; set; } = "";
    /// <summary>Hashed password (the plain password is never stored).</summary>
    [Required] public string PasswordHash { get; set; } = "";
    /// <summary>Role of the user.</summary>
    public UserRole Role { get; set; }
    /// <summary>Linked employee (for staff logins).</summary>
    public int? EmployeeId { get; set; }
    /// <summary>Linked customer (for customer logins).</summary>
    public int? CustomerId { get; set; }
    /// <summary>False blocks the login.</summary>
    public bool IsActive { get; set; } = true;
}
