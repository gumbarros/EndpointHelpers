using System.Diagnostics;
using EndpointHelpers.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace EndpointHelpers.Sample.Controllers;

[GenerateLinkGenerator]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy(int id, string aopba)
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}