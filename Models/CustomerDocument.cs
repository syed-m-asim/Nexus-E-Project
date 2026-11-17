using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>Address proof / application form / receipt filed by year and area (city).</summary>
public class CustomerDocument
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Customer the document belongs to.</summary>
    public int CustomerId { get; set; }
    /// <summary>Navigation property to the customer.</summary>
    public Customer? Customer { get; set; }
    /// <summary>Address proof, application form, receipt or other.</summary>
    public DocumentType DocumentType { get; set; }
    /// <summary>File name as uploaded by the user.</summary>
    [MaxLength(260)] public string OriginalFileName { get; set; } = "";
    /// <summary>Relative path of the stored file below wwwroot (year / city folders).</summary>
    [MaxLength(500)] public string StoredPath { get; set; } = "";
    /// <summary>Year under which the document is filed.</summary>
    public int FiledYear { get; set; }
    /// <summary>City / area under which the document is filed.</summary>
    public int? CityId { get; set; }
    /// <summary>Navigation property to the city.</summary>
    public City? City { get; set; }
    /// <summary>Date and time of the upload.</summary>
    public DateTime UploadedOn { get; set; } = DateTime.UtcNow;
    /// <summary>Employee who uploaded the document.</summary>
    public int UploadedByEmployeeId { get; set; }
}
