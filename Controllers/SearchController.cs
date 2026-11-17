using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Public status search. Anybody (no login) can look up an order by its 11 character order id,
/// a connection by its 16 character account id, or use the advanced search.
/// </summary>
[AllowAnonymous]
public class SearchController : Controller
{
    private readonly ApplicationDbContext _db;
    public SearchController(ApplicationDbContext db) => _db = db;

    /// <summary>Order id format: one type letter (D, B or T) followed by 10 digits, e.g. D0000000001.</summary>
    private static readonly Regex OrderIdFormat = new(@"^[DBT]\d{10}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Account id format: type letter + 3 digit city code + 12 digit serial = 16 characters.</summary>
    private static readonly Regex AccountIdFormat = new(@"^[DBT]\d{15}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Contact numbers may contain digits, spaces, +, -, ( and ).</summary>
    private static readonly Regex ContactFormat = new(@"^[0-9+\-\s()]{3,30}$", RegexOptions.Compiled);

    /// <summary>Shows the three search forms.</summary>
    public IActionResult Index() => View();

    /// <summary>Searches an order by its order id.</summary>
    [HttpGet]
    public async Task<IActionResult> Order(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            TempData["Error"] = "Please enter Order ID.";
            return RedirectToAction(nameof(Index));
        }

        // VALIDATION (added): reject ids that cannot exist before hitting the database.
        if (!OrderIdFormat.IsMatch(orderNumber.Trim()))
        {
            TempData["Error"] = "Invalid Order ID. It must be one letter (D, B or T) followed by 10 digits, e.g. D0000000001.";
            return RedirectToAction(nameof(Index));
        }

        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Plan)
            .Include(o => o.RetailStore)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber.Trim());
        if (order == null)
        {
            TempData["Error"] = "Order not found: " + orderNumber;
            return RedirectToAction(nameof(Index));
        }
        return View("OrderResult", order);
    }

    /// <summary>Searches a connection (status, due amount ...) by its account id.</summary>
    [HttpGet]
    public async Task<IActionResult> Account(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            TempData["Error"] = "Please enter Account ID.";
            return RedirectToAction(nameof(Index));
        }

        // VALIDATION (added): account id = 1 letter + 15 digits (3 digit city code + 12 digit serial).
        if (!AccountIdFormat.IsMatch(accountId.Trim()))
        {
            TempData["Error"] = "Invalid Account ID. It must be one letter (D, B or T) followed by 15 digits, e.g. D001000000000001.";
            return RedirectToAction(nameof(Index));
        }

        var conn = await _db.Connections
            .Include(c => c.Customer)
            .Include(c => c.Plan)
            .Include(c => c.Bills)
            .FirstOrDefaultAsync(c => c.AccountId == accountId.Trim());
        if (conn == null)
        {
            TempData["Error"] = "Connection not found: " + accountId;
            return RedirectToAction(nameof(Index));
        }
        return View("AccountResult", conn);
    }

    /// <summary>Advanced search by name, contact number and date range (orders and connections).</summary>
    [HttpPost]
    public async Task<IActionResult> Advanced(string? name, string? connectionType, string? contact, DateTime? fromDate, DateTime? toDate)
    {
        // VALIDATION (added): check every filter before running the queries.
        string? searchError = null;
        if (!string.IsNullOrWhiteSpace(name) && name.Trim().Length > 150)
            searchError = "Name cannot be longer than 150 characters.";
        else if (!string.IsNullOrWhiteSpace(contact) && !ContactFormat.IsMatch(contact.Trim()))
            searchError = "Contact number may contain only digits, spaces, +, - and brackets (3 to 30 characters).";
        else if (!string.IsNullOrWhiteSpace(connectionType)
                 && !new[] { "D", "B", "T", "DialUp", "Broadband", "TelephoneOnly" }
                        .Contains(connectionType.Trim(), StringComparer.OrdinalIgnoreCase))
            searchError = "Connection type must be D (dial-up), B (broadband) or T (telephone only).";
        else if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            searchError = "'From' date cannot be later than 'To' date.";
        if (searchError != null)
        {
            TempData["Error"] = searchError;
            return RedirectToAction(nameof(Index));
        }

        // Connection type filter: D / B / T (or the full name).
        ConnectionType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(connectionType))
        {
            var typeLetter = char.ToUpperInvariant(connectionType.Trim()[0]);
            typeFilter = typeLetter == 'D' ? ConnectionType.DialUp : typeLetter == 'B' ? ConnectionType.Broadband : ConnectionType.TelephoneOnly;
        }
        // "To date" is inclusive: everything before the start of the next day.
        DateTime? toEnd = toDate.HasValue ? toDate.Value.Date.AddDays(1) : null;

        var ordersQ = _db.Orders.Include(o => o.Customer).Include(o => o.Plan).AsQueryable();
        if (!string.IsNullOrWhiteSpace(name))
            ordersQ = ordersQ.Where(o => o.Customer!.Name.Contains(name));
        if (!string.IsNullOrWhiteSpace(contact))
            ordersQ = ordersQ.Where(o => o.Customer!.Phone.Contains(contact));
        if (fromDate.HasValue)
            ordersQ = ordersQ.Where(o => o.PlacedOn >= fromDate.Value);
        if (toDate.HasValue)
            ordersQ = ordersQ.Where(o => o.PlacedOn < toEnd!.Value);
        if (typeFilter.HasValue)
            ordersQ = ordersQ.Where(o => o.ConnectionType == typeFilter.Value);

        ViewBag.Orders = await ordersQ.OrderByDescending(o => o.PlacedOn).Take(50).ToListAsync();

        var connQ = _db.Connections.Include(c => c.Customer).Include(c => c.Plan).AsQueryable();
        if (!string.IsNullOrWhiteSpace(name))
            connQ = connQ.Where(c => c.Customer!.Name.Contains(name));
        if (!string.IsNullOrWhiteSpace(contact))
            connQ = connQ.Where(c => c.Customer!.Phone.Contains(contact));
        if (fromDate.HasValue)
            connQ = connQ.Where(c => c.ActivatedOn >= fromDate.Value);
        if (toDate.HasValue)
            connQ = connQ.Where(c => c.ActivatedOn < toEnd!.Value);
        if (typeFilter.HasValue)
            connQ = connQ.Where(c => c.ConnectionType == typeFilter.Value);

        ViewBag.Connections = await connQ.OrderByDescending(c => c.ActivatedOn).Take(50).ToListAsync();
        return View("AdvancedResult");
    }
}
