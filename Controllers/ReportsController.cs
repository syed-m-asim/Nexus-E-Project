using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Read-only management reports: dashboard counters, overdue bills, low stock and order status summary.
/// These actions take no user input, so no input validation is needed here.
/// </summary>
[Authorize(Roles = "Admin,Accounts,Technical")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ReportsController(ApplicationDbContext db) => _db = db;

    /// <summary>Dashboard with the main counters.</summary>
    public async Task<IActionResult> Index()
    {
        ViewBag.TotalCustomers = await _db.Customers.CountAsync();
        ViewBag.TotalOrders = await _db.Orders.CountAsync();
        ViewBag.ActiveConnections = await _db.Connections.CountAsync(c => c.Status == ConnectionStatus.Active);
        ViewBag.PendingOrders = await _db.Orders.CountAsync(o =>
            o.Status == OrderStatus.Placed || o.Status == OrderStatus.FeasibilityInProgress);
        ViewBag.UnpaidBills = await _db.Bills.CountAsync(b => b.Status != BillStatus.Paid);
        ViewBag.OverdueAmount = await _db.Bills
            .Where(b => b.Status != BillStatus.Paid && b.DueDate < DateTime.UtcNow)
            .SumAsync(b => (decimal?)b.BalanceDue) ?? 0;
        ViewBag.LowStock = await _db.Products.CountAsync(p => p.StockQty <= p.ReorderLevel);
        return View();
    }

    /// <summary>Unpaid bills whose due date has passed, oldest first.</summary>
    public async Task<IActionResult> OverdueBills()
    {
        var list = await _db.Bills
            .Include(b => b.Connection).ThenInclude(c => c!.Customer)
            .Where(b => b.Status != BillStatus.Paid && b.DueDate < DateTime.UtcNow)
            .OrderBy(b => b.DueDate)
            .ToListAsync();
        return View(list);
    }

    /// <summary>Products whose stock has fallen to (or below) the reorder level.</summary>
    public async Task<IActionResult> LowStock()
    {
        var list = await _db.Products
            .Include(p => p.Vendor)
            .Where(p => p.StockQty <= p.ReorderLevel)
            .OrderBy(p => p.StockQty)
            .ToListAsync();
        return View(list);
    }

    /// <summary>Number of orders per status.</summary>
    public async Task<IActionResult> OrderStatusSummary()
    {
        var summary = await _db.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        return View(summary);
    }

    /// <summary>
    /// Equipment demand from pending orders vs current stock.
    /// Shows how many units of each product are still needed for orders that are not yet fully provided.
    /// </summary>
    public async Task<IActionResult> EquipmentDemand()
    {
        var pendingOrders = await _db.Orders
            .Include(o => o.EquipmentProduct)
            .Where(o => o.EquipmentProductId != null &&
                (o.Status == OrderStatus.Placed || o.Status == OrderStatus.FeasibilityInProgress
                 || o.Status == OrderStatus.Feasible || o.Status == OrderStatus.PartiallyProvided))
            .ToListAsync();

        var demand = pendingOrders
            .GroupBy(o => o.EquipmentProductId!.Value)
            .Select(g => new
            {
                ProductId = g.Key,
                ProductName = g.First().EquipmentProduct?.Name ?? ("#" + g.Key),
                Needed = g.Sum(o => Math.Max(0, o.NumberOfConnections - o.ConnectionsProvided)),
                Stock = g.First().EquipmentProduct?.StockQty ?? 0
            })
            .Where(x => x.Needed > 0)
            .OrderByDescending(x => x.Needed)
            .ToList();

        ViewBag.Demand = demand;
        return View();
    }
}
