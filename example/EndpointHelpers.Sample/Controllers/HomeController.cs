using System.Diagnostics;
using EndpointHelpers;
using EndpointHelpers.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace EndpointHelpers.Sample.Controllers;

[GenerateLinkGenerator]
[GenerateRedirectToAction]
public partial class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
    
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
