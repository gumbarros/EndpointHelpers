# EndpointHelpers

EndpointHelpers is a Roslyn source generator that creates strongly-typed helpers for ASP.NET Core MVC URL generation and redirects. It generates:

- `IUrlHelper` helpers with action methods per controller.
- `LinkGenerator` helpers with `Get{Action}Path` methods, including `HttpContext` overloads.
- Extension properties on `IUrlHelper` and `LinkGenerator` to access the helpers.
- Redirect helpers for controller actions, so `RedirectToAction("Index")` becomes `this.RedirectToIndex()`.
- Attribute types used to control generation.

This package ships only a source generator and generated code. There is no runtime dependency.

## Example

### Without the generator

```razorhtmldialect
<a href='@Url.Action(action: "Details",controller: "Orders", values: new { orderId = 123, source = "dashboard" } )'>
    View order
</a>
```

### With the generator enabled
```razorhtmldialect
<a href='@Url.Orders.Details(123, "dashboard")'>
    View order
</a>
```

## Install

Add the NuGet package:

```xml
<ItemGroup>
  <PackageReference Include="EndpointHelpers" Version="1.0.4"/>
</ItemGroup>
```

or

```bash
dotnet add package EndpointHelpers
```

## Quick Start

Enable generation at the assembly level:

```csharp
using EndpointHelpers;

[assembly: GenerateUrlHelper]
[assembly: GenerateLinkGenerator]
[assembly: GenerateRedirectToAction]
```

Or enable all generators with a single attribute:

```csharp
using EndpointHelpers;

[assembly: GenerateEndpointHelpers]
```

Or apply to a specific controller:

```csharp
using EndpointHelpers;

[GenerateUrlHelper]
[GenerateLinkGenerator]
[GenerateRedirectToAction]
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
    [GenerateRedirectToAction]
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
}
```

Redirect example:

```csharp
public class OrdersController : Controller
{
    public IActionResult Index() => View();
    
    [GenerateRedirectToAction]
    public IActionResult Details(int orderId, string? source) => View();

    public IActionResult Save()
    {
        return this.RedirectToDetails(orderId: 123, source: "created");
    }
}
```

## Attributes and Scope

Generation can be enabled at different scopes:

- Assembly: `[assembly: GenerateUrlHelper]`, `[assembly: GenerateLinkGenerator]`, `[assembly: GenerateRedirectToAction]`, or `[assembly: GenerateEndpointHelpers]`.
- Controller: `[GenerateUrlHelper]`, `[GenerateLinkGenerator]`, `[GenerateRedirectToAction]`, or `[GenerateEndpointHelpers]` on the controller class.
- Action: `[GenerateUrlHelper]`, `[GenerateLinkGenerator]`, `[GenerateRedirectToAction]`, or `[GenerateEndpointHelpers]` on a specific action method.

You can exclude methods using:

- `[UrlHelperIgnore]`
- `[LinkGeneratorIgnore]`
- `[RedirectToActionIgnore]`
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
[GenerateRedirectToAction]
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
this.RedirectToDetails(orderId: 123, source: "dashboard");
```

## Example Project

See [`example/EndpointHelpers.Sample`](https://github.com/gumbarros/EndpointHelpers/tree/master/example/EndpointHelpers.Sample) for a minimal MVC app using all generators.

## License

GNU General Public License
