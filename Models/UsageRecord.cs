using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>
/// Hours used on an hourly Dial-up / Broadband plan. Technical/Accounts record usage; billed on next bill.
/// </summary>
public class UsageRecord
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public Connection? Connection { get; set; }

    [Range(0.01, 100000, ErrorMessage = "Hours must be greater than zero.")]
    [Display(Name = "Hours Used")]
    public decimal HoursUsed { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime RecordedOn { get; set; } = DateTime.UtcNow;
    public int RecordedByEmployeeId { get; set; }
    public int? BillId { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}
