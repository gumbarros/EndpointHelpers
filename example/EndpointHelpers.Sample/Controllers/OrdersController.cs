using System.Globalization;
using EndpointHelpers.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace EndpointHelpers.Sample.Controllers;

public sealed class OrdersController : Controller
{
    private static readonly List<Order> Orders = [];
    private static int _nextId = 1000;
    private static readonly Lock Gate = new();

    static OrdersController()
    {
        Seed();
    }

    public IActionResult Index(string? status = null, string? customer = null, string? q = null)
    {
        var query = Orders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(customer))
            query = query.Where(o => o.Customer.Equals(customer, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(o =>
                o.Customer.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                o.Status.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                o.Id.ToString(CultureInfo.InvariantCulture).Contains(q, StringComparison.OrdinalIgnoreCase));

        var list = query
            .OrderByDescending(o => o.CreatedUtc)
            .ToList();

        ViewData["Status"] = status ?? "";
        ViewData["Customer"] = customer ?? "";
        ViewData["Query"] = q ?? "";
        return View(list);
    }

    public IActionResult Details(int orderId, string? source = null)
    {
        var order = Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return NotFound();

        ViewData["Source"] = source ?? "";
        return View(order);
    }

    public IActionResult Create()
    {
        return View(new OrderEditModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(OrderEditModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var order = new Order
        {
            Id = NextId(),
            Customer = model.Customer,
            Status = model.Status,
            Total = model.Total,
            CreatedUtc = DateTime.UtcNow
        };

        Orders.Add(order);
        return RedirectToAction(nameof(Details), new { orderId = order.Id, source = "created" });
    }

    public IActionResult Edit(int orderId)
    {
        var order = Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return NotFound();

        var model = new OrderEditModel
        {
            Customer = order.Customer,
            Status = order.Status,
            Total = order.Total
        };

        ViewData["OrderId"] = orderId;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int orderId, OrderEditModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["OrderId"] = orderId;
            return View(model);
        }

        var order = Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return NotFound();

        order.Customer = model.Customer;
        order.Status = model.Status;
        order.Total = model.Total;

        return this.RedirectToDetails(orderId, "updated");
    }

    public IActionResult Delete(int orderId)
    {
        var order = Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return NotFound();

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int orderId)
    {
        var order = Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return NotFound();

        Orders.Remove(order);
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Recent(int days = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var list = Orders.Where(o => o.CreatedUtc >= cutoff)
            .OrderByDescending(o => o.CreatedUtc)
            .ToList();

        ViewData["Status"] = "";
        ViewData["Customer"] = "";
        ViewData["Query"] = "";
        ViewData["RecentDays"] = days;
        return View("Index", list);
    }

    public IActionResult ByStatus(string status)
    {
        return RedirectToAction(nameof(Index), new { status });
    }

    public IActionResult ByCustomer(string customer)
    {
        return RedirectToAction(nameof(Index), new { customer });
    }

    public IActionResult Search(string q)
    {
        return RedirectToAction(nameof(Index), new { q });
    }

    public IActionResult Export(string format = "csv")
    {
        var payload = $"export: {format}, count: {Orders.Count}";
        return Content(payload, "text/plain");
    }

    public IActionResult Clone(int orderId)
    {
        var order = Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return NotFound();

        var clone = new Order
        {
            Id = NextId(),
            Customer = order.Customer,
            Status = "Draft",
            Total = order.Total,
            CreatedUtc = DateTime.UtcNow
        };

        Orders.Add(clone);
        return this.RedirectToEdit(clone.Id);
    }

    private static int NextId()
    {
        lock (Gate)
        {
            return _nextId++;
        }
    }

    private static void Seed()
    {
        Orders.AddRange([
            new Order { Id = NextId(), Customer = "Northwind", Status = "Open", Total = 120.50m, CreatedUtc = DateTime.UtcNow.AddDays(-1) },
            new Order { Id = NextId(), Customer = "Contoso", Status = "Processing", Total = 845.00m, CreatedUtc = DateTime.UtcNow.AddDays(-2) },
            new Order { Id = NextId(), Customer = "Fabrikam", Status = "Shipped", Total = 310.25m, CreatedUtc = DateTime.UtcNow.AddDays(-4) },
            new Order { Id = NextId(), Customer = "AdventureWorks", Status = "Open", Total = 72.10m, CreatedUtc = DateTime.UtcNow.AddDays(-7) },
            new Order { Id = NextId(), Customer = "Spinella", Status = "Cancelled", Total = 19.99m, CreatedUtc = DateTime.UtcNow.AddDays(-12) }
        ]);
    }
}
