using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NexusCommunication.Models;

/// <summary>Feedback collected from a customer about an order or a connection (rating 1 to 5 plus optional comments).</summary>
public class Feedback
{
    public int Id { get; set; }

    /// <summary>The customer giving the feedback (existence / ownership is checked in FeedbacksController).</summary>
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public int? ConnectionId { get; set; }
    public Connection? Connection { get; set; }

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    [MaxLength(1000, ErrorMessage = "Comments cannot be longer than 1000 characters.")]
    public string? Comments { get; set; }

    [BindNever]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
