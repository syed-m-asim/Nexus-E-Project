using System.ComponentModel.DataAnnotations;

namespace NexusCommunication.Models;

/// <summary>
/// A provided connection. AccountId is 16 chars: type letter + 3-digit city code + 12-digit serial.
/// </summary>
public class Connection
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }
    /// <summary>Unique 16-character account id given to the customer.</summary>
    [MaxLength(16)] public string AccountId { get; set; } = "";
    /// <summary>Order from which this connection was created.</summary>
    public int OrderId { get; set; }
    /// <summary>Navigation property to the order.</summary>
    public Order? Order { get; set; }
    /// <summary>Owner of the connection.</summary>
    public int CustomerId { get; set; }
    /// <summary>Navigation property to the customer.</summary>
    public Customer? Customer { get; set; }
    /// <summary>City in which the connection is installed (its code is part of the account id).</summary>
    public int CityId { get; set; }
    /// <summary>Navigation property to the city.</summary>
    public City? City { get; set; }
    /// <summary>Dial-up, broadband or telephone only.</summary>
    public ConnectionType ConnectionType { get; set; }
    /// <summary>Plan currently chosen for the connection.</summary>
    public int PlanId { get; set; }
    /// <summary>Navigation property to the plan.</summary>
    public Plan? Plan { get; set; }
    /// <summary>Active, temporarily inactive or permanently inactive.</summary>
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Active;
    /// <summary>Date the connection was activated.</summary>
    public DateTime ActivatedOn { get; set; } = DateTime.UtcNow;
    /// <summary>Date the connection was made inactive (null while active).</summary>
    public DateTime? DeactivatedOn { get; set; }
    /// <summary>Bulk discount applied to the advance (first plan charge) and the security deposit.</summary>
    public decimal DiscountPercent { get; set; }
    /// <summary>Full (undiscounted) deposit for this connection type.</summary>
    public decimal SecurityDepositAmount { get; set; }
    /// <summary>True once the security deposit has been added to a bill.</summary>
    public bool SecurityDepositBilled { get; set; }
    /// <summary>Deposit actually charged after discount (set when the first bill is generated).</summary>
    public decimal SecurityDepositCharged { get; set; }
    /// <summary>Security deposit refunded on withdrawal / cancellation.</summary>
    public decimal? RefundAmount { get; set; }
    /// <summary>Date the deposit was refunded.</summary>
    public DateTime? RefundedOn { get; set; }
    /// <summary>Dial-up: the landline connection this internet connection runs on.</summary>
    public int? LinkedLandlineConnectionId { get; set; }
    /// <summary>Technical employee who created the connection.</summary>
    public int CreatedByEmployeeId { get; set; }
    /// <summary>Equipment (modem / router) provided with this connection.</summary>
    public List<ConnectionEquipment> Equipment { get; set; } = new();
    /// <summary>All status changes of this connection.</summary>
    public List<ConnectionStatusHistory> StatusHistory { get; set; } = new();
    /// <summary>Bills generated for this connection.</summary>
    public List<Bill> Bills { get; set; } = new();
}
