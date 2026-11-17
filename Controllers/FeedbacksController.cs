using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Customer feedback about orders / connections.
/// A logged-in customer can give feedback only as himself and only about his own orders.
/// Admin and retail employees can give feedback on behalf of any customer and read all feedback;
/// only the Admin can delete it.
/// </summary>
public class FeedbacksController : Controller
{
    private readonly ApplicationDbContext _db;
    public FeedbacksController(ApplicationDbContext db) => _db = db;

    /// <summary>Lists all feedback, newest first (Admin and retail employees).</summary>
    [Authorize(Roles = "Admin,RetailEmployee")]
    public async Task<IActionResult> Index()
    {
        var list = await _db.Feedbacks
            .Include(f => f.Customer)
            .Include(f => f.Order)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
        return View(list);
    }

    /// <summary>Shows the empty feedback form. A customer sees only his own name and his own orders.</summary>
    [Authorize(Roles = "Customer,Admin,RetailEmployee")]
    public async Task<IActionResult> Create()
    {
        var feedback = new Feedback { Rating = 5 };

        if (User.IsInRole("Customer"))
        {
            var own = await GetOwnCustomerAsync();
            if (own == null)
            {
                TempData["Error"] = "No customer profile is linked to your login e-mail. Please contact the retail outlet.";
                return RedirectToAction("Index", "MyPortal");
            }
            feedback.CustomerId = own.Id;
        }

        await LoadListsAsync(feedback);
        return View(feedback);
    }

    /// <summary>Saves the feedback after validating customer, order and rating.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Customer,Admin,RetailEmployee")]
    public async Task<IActionResult> Create(Feedback feedback)
    {
        // The form has no connection field, so a posted value is never accepted.
        feedback.ConnectionId = null;
        feedback.Comments = string.IsNullOrWhiteSpace(feedback.Comments) ? null : feedback.Comments.Trim();

        if (User.IsInRole("Customer"))
        {
            // A customer always gives feedback as himself (found through the login e-mail); the posted value is ignored.
            var own = await GetOwnCustomerAsync();
            if (own == null)
            {
                TempData["Error"] = "No customer profile is linked to your login e-mail. Please contact the retail outlet.";
                return RedirectToAction("Index", "MyPortal");
            }
            feedback.CustomerId = own.Id;
            ModelState.Remove(nameof(Feedback.CustomerId));
        }
        else
        {
            if (feedback.CustomerId <= 0)
                ModelState.AddModelError(nameof(Feedback.CustomerId), "Please select a customer.");
            else if (!await _db.Customers.AnyAsync(c => c.Id == feedback.CustomerId))
                ModelState.AddModelError(nameof(Feedback.CustomerId), "The selected customer does not exist.");
        }

        // The order (if any) must exist and must belong to the customer.
        if (feedback.OrderId.HasValue)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == feedback.OrderId.Value);
            if (order == null)
                ModelState.AddModelError(nameof(Feedback.OrderId), "The selected order does not exist.");
            else if (feedback.CustomerId > 0 && order.CustomerId != feedback.CustomerId)
                ModelState.AddModelError(nameof(Feedback.OrderId), User.IsInRole("Customer")
                    ? "You can only give feedback about your own orders."
                    : "The selected order does not belong to the selected customer.");
        }

        if (!ModelState.IsValid)
        {
            await LoadListsAsync(feedback);
            return View(feedback);
        }

        feedback.CreatedAt = DateTime.UtcNow;
        _db.Feedbacks.Add(feedback);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Thank you for your feedback!";
        if (User.IsInRole("Customer"))
            return RedirectToAction("Index", "MyPortal");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the delete confirmation page (Admin only).</summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var f = await _db.Feedbacks.FindAsync(id);
        if (f == null) return NotFound();
        return View(f);
    }

    /// <summary>Deletes the feedback (Admin only).</summary>
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var f = await _db.Feedbacks.FindAsync(id);
        if (f != null) { _db.Feedbacks.Remove(f); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The customer record that belongs to the logged-in customer (matched by login e-mail).</summary>
    private async Task<Customer?> GetOwnCustomerAsync()
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrEmpty(email)) return null;
        return await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);
    }

    /// <summary>
    /// Fills the drop-downs of the form.
    /// Customer login: no customer list (only his own name) and only his own orders.
    /// Staff: all customers (name + customer code) and all orders (order number + customer name).
    /// </summary>
    private async Task LoadListsAsync(Feedback feedback)
    {
        if (User.IsInRole("Customer"))
        {
            var own = await GetOwnCustomerAsync();
            ViewBag.IsCustomer = true;
            ViewBag.CustomerName = own?.Name ?? "";
            var ownId = own?.Id ?? 0;
            var orders = await _db.Orders
                .Where(o => o.CustomerId == ownId)
                .OrderByDescending(o => o.PlacedOn)
                .Select(o => new { o.Id, Text = o.OrderNumber })
                .ToListAsync();
            ViewBag.OrderId = new SelectList(orders, "Id", "Text", feedback.OrderId);
        }
        else
        {
            ViewBag.IsCustomer = false;
            var customers = await _db.Customers
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, Text = c.Name + " (" + c.CustomerCode + ")" })
                .ToListAsync();
            ViewBag.CustomerId = new SelectList(customers, "Id", "Text", feedback.CustomerId);

            var orders = await _db.Orders
                .OrderByDescending(o => o.PlacedOn)
                .Select(o => new { o.Id, Text = o.OrderNumber + " - " + o.Customer!.Name })
                .ToListAsync();
            ViewBag.OrderId = new SelectList(orders, "Id", "Text", feedback.OrderId);
        }
    }
}