using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EndpointHelpers.Tests;

public sealed class UrlHelperGeneratorTests
{
    private const string ControllerSource = """

                                            using Microsoft.AspNetCore.Mvc;
                                            using EndpointHelpers;

                                            namespace Test;

                                            [Area("Admin")]
                                            public class HomeController : Controller
                                            {
                                                [GenerateUrlHelper]
                                                public IActionResult Index(int id, string slug) => Ok();

                                                [GenerateUrlHelper]
                                                public IActionResult Details(int id) => Ok();
                                            }
                                            """;

    private const string OptionalAndIgnoredParametersSource = """

                                                              using System.Runtime.InteropServices;
                                                              using System.Threading;
                                                              using Microsoft.AspNetCore.Mvc;
                                                              using EndpointHelpers;

                                                              namespace Test;

                                                              public struct WidgetId
                                                              {
                                                                  public int Value { get; set; }
                                                              }

                                                              public class HomeController : Controller
                                                              {
                                                                  [GenerateUrlHelper]
                                                                  public IActionResult Index(
                                                                      int id,
                                                                      [FromServices] object service,
                                                                      CancellationToken cancellationToken,
                                                                      [Optional] WidgetId widgetId) => Ok();
                                                              }
                                                              """;

    [Fact]
    public void Generates_Controller_Helper_Class()
    {
        var generated = Run();

        Assert.Contains("public sealed class HomeControllerUrlHelper(IUrlHelper url)", generated);
    }

    [Fact]
    public void Generates_Action_Method_With_Parameters()
    {
        var generated = Run();

        Assert.Contains("public string Index(int id, string slug)", generated);
    }

    [Fact]
    public void Ignores_CancellationToken_And_FromServices_Parameters()
    {
        var generated = Run(OptionalAndIgnoredParametersSource);

        Assert.Contains("public string Index(int id, Test.WidgetId widgetId = default)", generated);
        Assert.DoesNotContain("CancellationToken", generated);
        Assert.DoesNotContain("FromServices", generated);
        Assert.DoesNotContain("\"service\"", generated);
        Assert.DoesNotContain("\"cancellationToken\"", generated);
    }

    [Fact]
    public void Uses_Default_For_Optional_Struct_Without_Explicit_Default()
    {
        var generated = Run(OptionalAndIgnoredParametersSource);

        Assert.Contains("Test.WidgetId widgetId = default", generated);
        Assert.DoesNotContain("Test.WidgetId widgetId = null", generated);
    }

    [Fact]
    public void Generates_Extension_Property_For_Controller()
    {
        var generated = Run();

        Assert.Contains("public HomeControllerUrlHelper Home", generated);
        Assert.Contains("=> new HomeControllerUrlHelper(url);", generated);
    }

    [Fact]
    public void Generates_Url_Action_Call()
    {
        var generated = Run();

        Assert.Contains("url.Action", generated);
        Assert.Contains("Index", generated);
        Assert.Contains("Home", generated);
    }

    private static string Run()
        => Run(ControllerSource);

    private static string Run(string source)
    {
        var generator = new UrlHelperGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Controller).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(AreaAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RouteValueAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(FromServicesAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.CancellationToken).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(OptionalAttribute).Assembly.Location)
            ]);

        var result = driver.RunGenerators(compilation).GetRunResult();

        return result.GeneratedTrees
            .Single(t => t.FilePath.EndsWith("UrlHelperExtensions.g.cs"))
            .GetText()
            .ToString();
    }
}
  
