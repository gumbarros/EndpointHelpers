using System.Linq;
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
                                            using UrlHelperGenerator;

                                            namespace Test;

                                            [Area("Admin")]
                                            public sealed class HomeController : Controller
                                            {
                                                [GenerateUrlHelper]
                                                public IActionResult Index(int id, string slug) => Ok();

                                                [GenerateUrlHelper]
                                                public IActionResult Details(int id) => Ok();
                                            }
                                            """;

    [Fact]
    public void Generates_Controller_Helper_Class()
    {
        var generated = Run();

        Assert.Contains("public class HomeControllerUrlHelper(IUrlHelper url)", generated);
    }

    [Fact]
    public void Generates_Action_Method_With_Parameters()
    {
        var generated = Run();

        Assert.Contains("public string Index(int id, string slug)", generated);
    }

    [Fact]
    public void Generates_Extension_Property_For_Controller()
    {
        var generated = Run();

        Assert.Contains("public HomeControllerUrlHelper Home", generated);
        Assert.Contains("return new HomeControllerUrlHelper(url);", generated);
    }

    [Fact]
    public void Generates_Url_Action_Call()
    {
        var generated = Run();

        Assert.Contains("return url.Action(\"Index\", \"Home\"", generated);
        Assert.Contains("return url.Action(\"Details\", \"Home\"", generated);
    }

    private static string Run()
    {
        var generator = new UrlHelperGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(ControllerSource)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Controller).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(AreaAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RouteValueAttribute).Assembly.Location)
            ]);

        var result = driver.RunGenerators(compilation).GetRunResult();

        return result.GeneratedTrees
            .Single(t => t.FilePath.EndsWith("UrlHelperExtensions.g.cs"))
            .GetText()
            .ToString();
    }
}
  