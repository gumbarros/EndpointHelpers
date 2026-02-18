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
  <PackageReference Include="EndpointHelpers" Version="1.0.5"/>
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
```

Or apply to a specific controller:

```csharp
using EndpointHelpers;

[GenerateUrlHelper]
[GenerateLinkGenerator]
[GenerateRedirectToAction]
public partial class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
}
```

Or only to a specific action:

```csharp
using EndpointHelpers;

public partial class HomeController : Controller
{
    [GenerateUrlHelper]
    [GenerateRedirectToAction]
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
}
```

Redirect example:

```csharp
public partial class OrdersController : Controller
{
    public IActionResult Index() => View();
    
    [GenerateRedirectToAction]
    public IActionResult Details(int orderId, string? source) => View();

    public IActionResult Save()
    {
        return RedirectToDetails(orderId: 123, source: "created");
    }
}
```

## Attributes and Scope

Generation can be enabled at different scopes:

- Assembly: `[assembly: GenerateUrlHelper]`, `[assembly: GenerateLinkGenerator]`.
- Controller: `[GenerateUrlHelper]`, `[GenerateLinkGenerator]`, `[GenerateRedirectToAction]` on the controller class.
- Action: `[GenerateUrlHelper]`, `[GenerateLinkGenerator]`, `[GenerateRedirectToAction]` on a specific action method.
- `GenerateRedirectToAction` does not support assembly-level attributes.
- Controllers must be declared `partial` when using `GenerateRedirectToAction`.

You can exclude methods using:

- `[UrlHelperIgnore]`
- `[LinkGeneratorIgnore]`
- `[RedirectToActionIgnore]`
- `[NonAction]` (standard ASP.NET Core MVC attribute)

## Behavior

- Controllers are discovered by name: non-abstract classes ending with `Controller`.
- Only public, ordinary methods are included.
- UrlHelper and LinkGenerator helpers are placed in the `EndpointHelpers` namespace.
- Redirect helpers are generated as `partial` members in the controller namespace.
- Extension properties use the controller name without the `Controller` suffix.

### Controller and action discovery
```csharp
[GenerateUrlHelper]
[GenerateLinkGenerator]
[GenerateRedirectToAction]
public partial class OrdersController : Controller
{
    public IActionResult Index() => View();
    
    public IActionResult Details(int orderId, string? source) => View();
}
```


#### Generated surface

```csharp
// UrlHelperGenerator

Url.Orders.Index();
Url.Orders.Details(orderId: 123, source: "dashboard");

// LinkGeneratorGenerator

LinkGenerator.Orders.GetIndexPath();
LinkGenerator.Orders.GetDetailsPath(123, "dashboard");

// RedirectToActionGenerator

this.RedirectToIndex();
this.RedirectToDetails(orderId: 123, source: "dashboard");
```

## Example Project

See [`example/EndpointHelpers.Sample`](https://github.com/gumbarros/EndpointHelpers/tree/master/example/EndpointHelpers.Sample) for a minimal MVC app using all generators.

## License

GNU General Public License
