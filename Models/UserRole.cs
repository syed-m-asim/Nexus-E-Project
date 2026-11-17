namespace NexusCommunication.Models;

/// <summary>Roles of the application. The role decides which menus and actions a logged-in user can use.</summary>
public enum UserRole
{
    /// <summary>Manager: plans, employees, stores, vendors, stock, purchase orders.</summary>
    Admin = 1,
    /// <summary>Retail outlet staff: customers, orders, payments.</summary>
    RetailEmployee = 2,
    /// <summary>Technical staff: feasibility, connections, equipment.</summary>
    Technical = 3,
    /// <summary>Accounts department: bills and payments.</summary>
    Accounts = 4,
    /// <summary>Customer using the self-service portal.</summary>
    Customer = 5
}
