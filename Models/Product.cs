using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NexusCommunication.Models;

/// <summary>Equipment such as modems / routers / handsets, including stock tracking.</summary>
public class Product : IEntity, IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required.")]
    [MinLength(2, ErrorMessage = "Product name must be at least 2 characters long.")]
    [MaxLength(150, ErrorMessage = "Product name cannot be longer than 150 characters.")]
    public string Name { get; set; } = "";

    [EnumDataType(typeof(ProductCategory), ErrorMessage = "Select a valid category.")]
    public ProductCategory Category { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot be longer than 500 characters.")]
    public string? Description { get; set; }

    /// <summary>Monthly rental charged to the customer while the equipment is with them.</summary>
    [Range(0.0, 1000000.0, ErrorMessage = "Monthly rental must be between 0 and 1,000,000.")]
    [Display(Name = "Monthly Rental")]
    public decimal MonthlyRental { get; set; }

    /// <summary>Charge levied when the customer damages the equipment and it is replaced.</summary>
    [Range(0.0, 10000000.0, ErrorMessage = "Replacement charge must be between 0 and 10,000,000.")]
    [Display(Name = "Replacement Charge")]
    public decimal ReplacementCharge { get; set; }

    /// <summary>Units currently in stock. Can never be negative.</summary>
    [Range(0, 10000000, ErrorMessage = "Stock quantity must be between 0 and 10,000,000.")]
    [Display(Name = "Stock Quantity")]
    public int StockQty { get; set; }

    /// <summary>When stock falls to this level it appears in the low-stock report.</summary>
    [Range(0, 10000000, ErrorMessage = "Reorder level must be between 0 and 10,000,000.")]
    [Display(Name = "Reorder Level")]
    public int ReorderLevel { get; set; }

    /// <summary>Web path of the uploaded product picture, e.g. /uploads/products/abc.jpg (null = no image).
    /// [BindNever]: it is set only by ProductsController after the file passed validation, never by the posted form.</summary>
    [BindNever]
    [MaxLength(260)]
    [Display(Name = "Image")]
    public string? ImagePath { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    /// <summary>The vendor column stays optional in the database, but a product on the form must have a vendor.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!VendorId.HasValue) yield return new ValidationResult("Please select a vendor.", new[] { nameof(VendorId) });
    }
}
