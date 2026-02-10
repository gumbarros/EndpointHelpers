# EndpointHelpers

EndpointHelpers is a Roslyn source generator that creates strongly-typed helpers for ASP.NET Core MVC URL generation. It generates:

- `IUrlHelper` helpers with action methods per controller.
- `LinkGenerator` helpers with `Get{Action}Path` methods, including `HttpContext` overloads.
- Extension properties on `IUrlHelper` and `LinkGenerator` to access the helpers.
- Attribute types used to control generation.

This package ships only a source generator and generated code. There is no runtime dependency.

## Example

### Without the generator

```razorhtmldialect
<a href="@Url.Action(
        action: "Details",
        controller: "Orders",
        values: new { orderId = 123, source = "dashboard" }
    )">
    View order
</a>
```

### With the generator enabled
```razorhtmldialect
<a href="@Url.Orders.Details(123, "dashboard")">
View order
</a>
```

## Install

Add the NuGet package:

```xml
<ItemGroup>
  <PackageReference Include="EndpointHelpers" Version="1.0.1"/>
</ItemGroup>
```

## Quick Start

Enable generation at the assembly level:

```csharp
using EndpointHelpers;

[assembly: GenerateUrlHelper]
[assembly: GenerateLinkGenerator]
```

Or apply to a specific controller:

```csharp
using EndpointHelpers;

[GenerateUrlHelper]
[GenerateLinkGenerator]
public class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
}
```

Or only to a specific action:

```csharp
using EndpointHelpers;

public class HomeController : Controller
{
    [GenerateUrlHelper]
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
}
```

## Attributes and Scope

Generation can be enabled at different scopes:

- Assembly: `[assembly: GenerateUrlHelper]` or `[assembly: GenerateLinkGenerator]`.
- Controller: `[GenerateUrlHelper]` or `[GenerateLinkGenerator]` on the controller class.
- Action: `[GenerateUrlHelper]` or `[GenerateLinkGenerator]` on a specific action method.

You can exclude methods using:

- `[UrlHelperIgnore]`
- `[LinkGeneratorIgnore]`
- `[NonAction]` (standard ASP.NET Core MVC attribute)

## Behavior

- Controllers are discovered by name: non-abstract classes ending with `Controller`.
- Only public, ordinary methods are included.
- Generated helpers are placed in the `EndpointHelpers` namespace.
- Extension properties use the controller name without the `Controller` suffix.

### Controller and action discovery
```csharp
[GenerateUrlHelper]
[GenerateLinkGenerator]
public class OrdersController : Controller
{
    public IActionResult Index() => View();
    
    public IActionResult Details(int orderId, string? source) => View();
}
```


Generated surface
```csharp
Url.Orders.Index();
Url.Orders.Details(orderId: 123, source: "dashboard");
LinkGenerator.Orders.GetDetailsPath(123, "dashboard");
LinkGenerator.Orders.GetIndexPath();
LinkGenerator.Orders.GetDetailsPath(httpContext, 123, "dashboard");
```

## Example Project

See [`example/EndpointHelpers.Sample`](https://github.com/gumbarros/EndpointHelpers/tree/master/example/EndpointHelpers.Sample) for a minimal MVC app using both generators.

## License

GNU General Public License
