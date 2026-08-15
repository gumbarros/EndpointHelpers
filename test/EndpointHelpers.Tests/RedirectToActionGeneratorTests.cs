using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EndpointHelpers.Tests;

public sealed class RedirectToActionGeneratorTests
{
    private const string ControllerSource = """

                                            using Microsoft.AspNetCore.Mvc;

                                            namespace EndpointHelpers
                                            {
                                                [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class | System.AttributeTargets.Assembly)]
                                                public sealed class GenerateRedirectToActionAttribute : System.Attribute;
                                            }

                                            namespace Test
                                            {
                                                using EndpointHelpers;

                                                public partial class HomeController : Controller
                                                {
                                                    [GenerateRedirectToAction]
                                                    public IActionResult Index(int id, string slug) => Ok();

                                                    [GenerateRedirectToAction]
                                                    public IActionResult Details(int id) => Ok();
                                                }
                                            }
                                            """;

    private const string AreaControllerSource = """

                                                using Microsoft.AspNetCore.Mvc;

                                                namespace EndpointHelpers
                                                {
                                                    [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class | System.AttributeTargets.Assembly)]
                                                    public sealed class GenerateRedirectToActionAttribute : System.Attribute;
                                                }

                                                namespace Test
                                                {
                                                    using EndpointHelpers;

                                                    [Area("Admin")]
                                                    public partial class UsersController : Controller
                                                    {
                                                        [GenerateRedirectToAction]
                                                        public IActionResult Index(int id) => Ok();
                                                    }
                                                }
                                                """;

    [Fact]
    public void Generates_Partial_Controller_Block()
    {
        var generated = Run();

        Assert.Contains("public partial class HomeController", generated);
    }

    [Fact]
    public void Generates_Action_Method_With_Parameters()
    {
        var generated = Run();

        Assert.Contains("protected RedirectToActionResult RedirectToIndex(int id, string slug)", generated);
    }

    [Fact]
    public void Generates_RouteValueDictionary_For_Parameters()
    {
        var generated = Run();

        Assert.Contains("new RouteValueDictionary", generated);
        Assert.Contains("{ \"id\", id }", generated);
        Assert.Contains("{ \"slug\", slug }", generated);
    }

    [Fact]
    public void Generates_RedirectToAction_Call()
    {
        var generated = Run();

        Assert.Contains("return RedirectToAction(", generated);
        Assert.Contains("\"Index\",", generated);
        Assert.Contains("\"Home\",", generated);
    }

    [Fact]
    public void Includes_Area_In_Redirect()
    {
        var generated = Run(AreaControllerSource);

        Assert.Contains("{ \"Area\", \"Admin\" }", generated);
        Assert.Contains("\"Users\",", generated);
    }

    private static string Run()
        => Run(ControllerSource);

    private static string Run(string source)
    {
        var generator = new RedirectToActionGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Controller).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RedirectToActionResult).Assembly.Location)
            ]);

        var result = driver.RunGenerators(compilation).GetRunResult();

        return result.GeneratedTrees
            .Single(tree => tree.FilePath.Contains("RedirectToActionControllers"))
            .GetText()
            .ToString();
    }
}
