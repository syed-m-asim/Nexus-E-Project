using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NexusCommunication.Models;

namespace NexusCommunication.Controllers;

/// <summary>
/// Public landing pages of the site (home page, privacy page and the common error page).
/// No login is required for any action here.
/// </summary>
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>Home page: shows tiles that depend on the role of the logged-in user.</summary>
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>Static privacy page.</summary>
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>Common error page. Caching is switched off so an old error is never shown again.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
