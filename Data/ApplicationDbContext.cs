using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using NexusCommunication.Models;

namespace NexusCommunication.Data;

/// <summary>
/// Entity Framework Core database context. Extends IdentityDbContext so the Identity tables (users, roles)
/// and all application tables live in one database (NexusDB).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ---- Master data (maintained by the Admin) ----
    public DbSet<City> Cities => Set<City>();
    public DbSet<RetailStore> RetailStores => Set<RetailStore>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<VendorPayment> VendorPayments => Set<VendorPayment>();
    // ---- Plans, deposits and bulk-discount scheme ----
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<SecurityDepositRate> SecurityDepositRates => Set<SecurityDepositRate>();
    public DbSet<BulkDiscountTier> BulkDiscountTiers => Set<BulkDiscountTier>();
    // ---- Customers, orders and connections ----
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<FeasibilityCheck> FeasibilityChecks => Set<FeasibilityCheck>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<ConnectionEquipment> ConnectionEquipments => Set<ConnectionEquipment>();
    public DbSet<ConnectionStatusHistory> ConnectionStatusHistories => Set<ConnectionStatusHistory>();
    public DbSet<ConnectionPlanChange> ConnectionPlanChanges => Set<ConnectionPlanChange>();
    public DbSet<ReplacementCharge> ReplacementCharges => Set<ReplacementCharge>();
    // ---- Billing and payments ----
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillLineItem> BillLineItems => Set<BillLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    // ---- Feedback and serial counters ----
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<SequenceCounter> SequenceCounters => Set<SequenceCounter>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

    /// <summary>All money columns use decimal(18,2).</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    /// <summary>Keys, unique indexes, delete behaviour and seed data.</summary>
    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        // Keys: these two tables use a natural key instead of an identity column.
        m.Entity<SequenceCounter>().HasKey(x => x.Key);
        m.Entity<SecurityDepositRate>().HasKey(x => x.ConnectionType);
        m.Entity<SecurityDepositRate>().Property(x => x.ConnectionType).ValueGeneratedNever();

        // Unique indexes: city code, order number, account id, bill number, receipt number and PO number must never repeat.
        m.Entity<City>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Order>().HasIndex(x => x.OrderNumber).IsUnique();
        m.Entity<Connection>().HasIndex(x => x.AccountId).IsUnique();
        m.Entity<Bill>().HasIndex(x => x.BillNumber).IsUnique();
        // Tax rate is a fraction with 4 decimals (0.1224 = 12.24%); the global rule would round it to 2 decimals.
        m.Entity<Bill>().Property(x => x.TaxRate).HasPrecision(18, 4);
        // Unique only when a real code is assigned (empty string allowed during migration of old rows).
        m.Entity<Customer>().HasIndex(x => x.CustomerCode)
            .IsUnique()
            .HasFilter("[CustomerCode] <> ''");
        m.Entity<Payment>().HasIndex(x => x.ReceiptNumber).IsUnique();
        m.Entity<PurchaseOrder>().HasIndex(x => x.PoNumber).IsUnique();

        // Restrict deletes everywhere: a record that is referenced by another record cannot be deleted by accident.
        foreach (var fk in m.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        // Exception: bill lines and purchase order lines are deleted together with their parent.
        m.Entity<BillLineItem>().HasOne<Bill>().WithMany(b => b.LineItems)
            .HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<PurchaseOrderItem>().HasOne<PurchaseOrder>().WithMany(p => p.Items)
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);

        SeedReferenceData(m);
    }


    /// <summary>Seeds the security deposits, the bulk-discount tiers and the plans given in the specification.</summary>
    private static void SeedReferenceData(ModelBuilder m)
    {
        const ConnectionType D = ConnectionType.DialUp;
        const ConnectionType B = ConnectionType.Broadband;
        const ConnectionType T = ConnectionType.TelephoneOnly;

        // Refundable security deposit: Dial-up 325, Broadband 500, Landline 250.
        m.Entity<SecurityDepositRate>().HasData(
            new SecurityDepositRate { ConnectionType = D, Amount = 325 },
            new SecurityDepositRate { ConnectionType = B, Amount = 500 },
            new SecurityDepositRate { ConnectionType = T, Amount = 250 });

        // Bulk / corporate scheme: 10-14 connections 25%, 15-24 50%, 25-49 75%, 50 and above 100%.
        m.Entity<BulkDiscountTier>().HasData(
            new BulkDiscountTier { Id = 1, MinConnections = 10, MaxConnections = 14, DiscountPercent = 25 },
            new BulkDiscountTier { Id = 2, MinConnections = 15, MaxConnections = 24, DiscountPercent = 50 },
            new BulkDiscountTier { Id = 3, MinConnections = 25, MaxConnections = 49, DiscountPercent = 75 },
            new BulkDiscountTier { Id = 4, MinConnections = 50, MaxConnections = 9999, DiscountPercent = 100 });

        // Plans 1-7 dial-up, 8-13 broadband, 14-18 landline (plan 18 has no price in the specification, so it is inactive).
        m.Entity<Plan>().HasData(
            new Plan { Id = 1, ConnectionType = D, PlanGroup = "Hourly Basis", Name = "Dial-Up 10 Hrs", Price = 50, ValidityDays = 30, Hours = 10 },
            new Plan { Id = 2, ConnectionType = D, PlanGroup = "Hourly Basis", Name = "Dial-Up 30 Hrs", Price = 130, ValidityDays = 90, Hours = 30 },
            new Plan { Id = 3, ConnectionType = D, PlanGroup = "Hourly Basis", Name = "Dial-Up 60 Hrs", Price = 260, ValidityDays = 180, Hours = 60 },
            new Plan { Id = 4, ConnectionType = D, PlanGroup = "Unlimited 28Kbps", Name = "Dial-Up Unlimited 28Kbps Monthly", Price = 75, ValidityDays = 30, SpeedKbps = 28 },
            new Plan { Id = 5, ConnectionType = D, PlanGroup = "Unlimited 28Kbps", Name = "Dial-Up Unlimited 28Kbps Quarterly", Price = 150, ValidityDays = 90, SpeedKbps = 28 },
            new Plan { Id = 6, ConnectionType = D, PlanGroup = "Unlimited 56Kbps", Name = "Dial-Up Unlimited 56Kbps Monthly", Price = 100, ValidityDays = 30, SpeedKbps = 56 },
            new Plan { Id = 7, ConnectionType = D, PlanGroup = "Unlimited 56Kbps", Name = "Dial-Up Unlimited 56Kbps Quarterly", Price = 180, ValidityDays = 90, SpeedKbps = 56 },
            new Plan { Id = 8, ConnectionType = B, PlanGroup = "Hourly Basis", Name = "Broadband 30 Hrs", Price = 175, ValidityDays = 30, Hours = 30 },
            new Plan { Id = 9, ConnectionType = B, PlanGroup = "Hourly Basis", Name = "Broadband 60 Hrs", Price = 315, ValidityDays = 180, Hours = 60 },
            new Plan { Id = 10, ConnectionType = B, PlanGroup = "Unlimited 64Kbps", Name = "Broadband Unlimited 64Kbps Monthly", Price = 225, ValidityDays = 30, SpeedKbps = 64 },
            new Plan { Id = 11, ConnectionType = B, PlanGroup = "Unlimited 64Kbps", Name = "Broadband Unlimited 64Kbps Quarterly", Price = 400, ValidityDays = 90, SpeedKbps = 64 },
            new Plan { Id = 12, ConnectionType = B, PlanGroup = "Unlimited 128Kbps", Name = "Broadband Unlimited 128Kbps Monthly", Price = 350, ValidityDays = 30, SpeedKbps = 128 },
            new Plan { Id = 13, ConnectionType = B, PlanGroup = "Unlimited 128Kbps", Name = "Broadband Unlimited 128Kbps Quarterly", Price = 445, ValidityDays = 90, SpeedKbps = 128 },
            new Plan { Id = 14, ConnectionType = T, PlanGroup = "Local", Name = "Landline Local Unlimited (Yearly Rental)", Price = 75, ValidityDays = 365, LocalCallRate = 0.55m },
            new Plan { Id = 15, ConnectionType = T, PlanGroup = "Local", Name = "Landline Local Monthly", Price = 35, ValidityDays = 30, LocalCallRate = 0.75m },
            new Plan { Id = 16, ConnectionType = T, PlanGroup = "STD", Name = "Landline STD Monthly", Price = 125, ValidityDays = 30, LocalCallRate = 0.70m, StdCallRate = 2.25m, MobileMessageRate = 1.00m },
            new Plan { Id = 17, ConnectionType = T, PlanGroup = "STD", Name = "Landline STD Half-Yearly", Price = 420, ValidityDays = 180, LocalCallRate = 0.60m, StdCallRate = 2.00m, MobileMessageRate = 1.15m },
            new Plan { Id = 18, ConnectionType = T, PlanGroup = "STD", Name = "Landline STD Yearly", Price = 0, ValidityDays = 365, LocalCallRate = 0.60m, StdCallRate = 1.75m, MobileMessageRate = 1.25m, IsActive = false }
        );
    }
}
