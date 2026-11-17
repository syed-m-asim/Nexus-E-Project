using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Filing of customer documents (address proofs, application forms, receipts) year-wise and area (city) wise.
/// Files are stored below wwwroot/uploads/documents/{year}/{cityId}/.
/// </summary>
[Authorize(Roles = "Admin,RetailEmployee")]
public class DocumentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    /// <summary>Largest file that may be uploaded (5 MB).</summary>
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    /// <summary>File types that may be filed (address proofs, forms and receipts are PDFs, scans or photos).</summary>
    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };

    public DocumentsController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>Lists filed documents, optionally filtered by customer, year and city.</summary>
    public async Task<IActionResult> Index(int? customerId, int? year, int? cityId)
    {
        var q = _db.CustomerDocuments
            .Include(d => d.Customer).Include(d => d.City)
            .AsQueryable();
        if (customerId.HasValue) q = q.Where(d => d.CustomerId == customerId);
        if (year.HasValue) q = q.Where(d => d.FiledYear == year);
        if (cityId.HasValue) q = q.Where(d => d.CityId == cityId);

        ViewBag.CustomerId = new SelectList(await _db.Customers.ToListAsync(), "Id", "Name", customerId);
        ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name", cityId);
        return View(await q.OrderByDescending(d => d.UploadedOn).ToListAsync());
    }

    /// <summary>Shows the upload form.</summary>
    public async Task<IActionResult> Upload(int? customerId)
    {
        ViewBag.CustomerId = new SelectList(await _db.Customers.ToListAsync(), "Id", "Name", customerId);
        ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name");
        return View();
    }

    /// <summary>Validates and stores the uploaded file and creates the filing record.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(int customerId, DocumentType documentType, int filedYear, int? cityId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("", "Please select a file.");
            ViewBag.CustomerId = new SelectList(await _db.Customers.ToListAsync(), "Id", "Name", customerId);
            ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name", cityId);
            return View();
        }

        // VALIDATION (added): file size / type, customer, city, document type and year.
        if (file.Length > MaxFileSizeBytes)
            ModelState.AddModelError("", "The file is too large. The maximum size is 5 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            ModelState.AddModelError("", "File type not allowed. Upload a PDF, JPG, PNG, DOC or DOCX file.");
        if (file.FileName.Length > 260)
            ModelState.AddModelError("", "The file name is too long (maximum 260 characters).");
        if (!await _db.Customers.AnyAsync(c => c.Id == customerId))
            ModelState.AddModelError("", "Please select a valid customer.");
        if (cityId.HasValue && !await _db.Cities.AnyAsync(c => c.Id == cityId.Value))
            ModelState.AddModelError("", "The selected city does not exist.");
        if (!Enum.IsDefined(typeof(DocumentType), documentType))
            ModelState.AddModelError("", "Select a valid document type.");
        if (filedYear < 2000 || filedYear > DateTime.UtcNow.Year + 1)
            ModelState.AddModelError("", $"Filed year must be between 2000 and {DateTime.UtcNow.Year + 1}.");
        if (!ModelState.IsValid)
        {
            ViewBag.CustomerId = new SelectList(await _db.Customers.ToListAsync(), "Id", "Name", customerId);
            ViewBag.CityId = new SelectList(await _db.Cities.ToListAsync(), "Id", "Name", cityId);
            return View();
        }

        // Folder structure: uploads/documents/<year>/<cityId>
        var folder = Path.Combine(_env.WebRootPath, "uploads", "documents", filedYear.ToString(), (cityId ?? 0).ToString());
        Directory.CreateDirectory(folder);
        // A GUID prefix keeps stored names unique; Path.GetFileName removes any path that the browser may send.
        var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var path = Path.Combine(folder, safeName);
        using (var stream = new FileStream(path, FileMode.Create))
            await file.CopyToAsync(stream);

        var relative = $"/uploads/documents/{filedYear}/{cityId ?? 0}/{safeName}";
        _db.CustomerDocuments.Add(new CustomerDocument
        {
            CustomerId = customerId,
            DocumentType = documentType,
            OriginalFileName = file.FileName,
            StoredPath = relative,
            FiledYear = filedYear,
            CityId = cityId,
            UploadedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Document filed successfully.";
        return RedirectToAction(nameof(Index), new { customerId, year = filedYear, cityId });
    }

    /// <summary>Shows the delete confirmation page.</summary>
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.CustomerDocuments.FindAsync(id);
        if (d == null) return NotFound();
        return View(d);
    }

    /// <summary>Deletes the filing record and the physical file.</summary>
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var d = await _db.CustomerDocuments.FindAsync(id);
        if (d != null)
        {
            var physical = Path.Combine(_env.WebRootPath, d.StoredPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physical))
                System.IO.File.Delete(physical);
            _db.CustomerDocuments.Remove(d);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
