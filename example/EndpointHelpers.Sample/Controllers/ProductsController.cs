using EndpointHelpers;
using EndpointHelpers.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace EndpointHelpers.Sample.Controllers;

[GenerateViewHelpers]
public sealed partial class ProductsController : Controller
{
    private static readonly IReadOnlyList<Product> Products =
    [
        new() { Id = 1, Name = "Aurora Desk Lamp", Category = "Lighting", Price = 79.99m, Rating = 4.7, Featured = true, Summary = "Minimalist dimmable desk lamp with USB-C." },
        new() { Id = 2, Name = "Nimbus Standing Desk", Category = "Furniture", Price = 549.00m, Rating = 4.8, Featured = true, Summary = "Dual-motor sit/stand desk with memory presets." },
        new() { Id = 3, Name = "Lumen LED Strip", Category = "Lighting", Price = 39.00m, Rating = 4.5, Featured = false, Summary = "Smart RGBW strip with Matter support." },
        new() { Id = 4, Name = "Cascade Ergonomic Chair", Category = "Furniture", Price = 429.00m, Rating = 4.6, Featured = true, Summary = "Adjustable lumbar, 4D arms, mesh back." },
        new() { Id = 5, Name = "Pulse Bluetooth Speaker", Category = "Audio", Price = 129.00m, Rating = 4.3, Featured = false, Summary = "Water-resistant portable speaker with 20h battery." },
        new() { Id = 6, Name = "Vertex Monitor Arm", Category = "Accessories", Price = 119.00m, Rating = 4.4, Featured = false, Summary = "Gas-spring arm, 32” max, built-in cable routing." },
        new() { Id = 7, Name = "Halo Task Light", Category = "Lighting", Price = 99.00m, Rating = 4.2, Featured = false, Summary = "Wide-beam task light with adjustable warmth." },
        new() { Id = 8, Name = "Echo USB Hub", Category = "Accessories", Price = 59.00m, Rating = 4.1, Featured = false, Summary = "7-port USB-C/USB-A hub with 100W PD passthrough." },
        new() { Id = 9, Name = "Chroma Desk Mat", Category = "Accessories", Price = 35.00m, Rating = 4.0, Featured = false, Summary = "Anti-slip vegan leather desk mat with stitched edges." },
        new() { Id = 10, Name = "Summit Studio Monitors", Category = "Audio", Price = 699.00m, Rating = 4.9, Featured = true, Summary = "Bi-amped nearfields tuned for small rooms." }
    ];

    public IActionResult Index(string? category = null, bool? featured = null)
    {
        var query = Products.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

        if (featured is true)
            query = query.Where(p => p.Featured);

        ViewData["Category"] = category ?? "";
        ViewData["Featured"] = featured == true;

        return IndexView(query.ToList());
    }

    public IActionResult Details(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product is null)
            return NotFound();
 
        return DetailsView(product);
    }

    // Show reuse of the Index view with an alternate template and explicit view name.
    public IActionResult Grid()
    {
        return GridView( Products);
    }

    // Show the overload that passes only a custom view name.
    public IActionResult Landing()
    {
        var featureSet = Products.Where(p => p.Featured).Take(3).ToList();
        return LandingView(featureSet);
    }
}
