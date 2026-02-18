using EndpointHelpers;
using EndpointHelpers.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace EndpointHelpers.Sample.Areas.Admin.Controllers;

[Area("Admin")]
[GenerateViewHelpers]
public sealed partial class UsersController : Controller
{
    private static readonly IReadOnlyList<AdminUser> Users =
    [
        new() { Id = 1, Name = "Avery Shaw", Email = "avery.shaw@example.test", Role = "Administrator", IsActive = true },
        new() { Id = 2, Name = "Kai Morgan", Email = "kai.morgan@example.test", Role = "Manager", IsActive = true },
        new() { Id = 3, Name = "Riley Quinn", Email = "riley.quinn@example.test", Role = "Support", IsActive = false },
        new() { Id = 4, Name = "Jordan Lee", Email = "jordan.lee@example.test", Role = "Auditor", IsActive = true }
    ];

    public IActionResult Index(string? role = null, bool? active = null)
    {
        var query = Users.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase));

        if (active is not null)
            query = query.Where(u => u.IsActive == active.Value);

        ViewData["Role"] = role ?? "";
        ViewData["Active"] = active?.ToString() ?? "";

        return IndexView(query.ToList());
    }

    public IActionResult Details(int id)
    {
        var user = Users.FirstOrDefault(u => u.Id == id);
        if (user is null)
            return NotFound();

        return DetailsView(user);
    }
}
