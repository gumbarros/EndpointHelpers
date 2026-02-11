using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EndpointHelpers.Tests;

public sealed class LinkGeneratorGeneratorTests
{
    private const string ControllerSource = """

                                            using Microsoft.AspNetCore.Mvc;

                                            namespace EndpointHelpers
                                            {
                                                [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class | System.AttributeTargets.Assembly)]
                                                public sealed class GenerateLinkGeneratorAttribute : System.Attribute;
                                            }

                                            namespace Test
                                            {
                                                using EndpointHelpers;

                                                public class HomeController : Controller
                                                {
                                                    [GenerateLinkGenerator]
                                                    public IActionResult Index(int id, string slug) => Ok();

                                                    [GenerateLinkGenerator]
                                                    public IActionResult Details(int id) => Ok();
                                                }
                                            }
                                            """;

    [Fact]
    public void Generates_Controller_Helper_Class()
    {
        var generated = Run();

        Assert.Contains("public sealed class HomeControllerLinkGenerator(LinkGenerator linkGenerator)", generated);
    }

    [Fact]
    public void Generates_Action_Method_With_Parameters()
    {
        var generated = Run();

        Assert.Contains("public string GetIndexPath(int id, string slug)", generated);
    }

    [Fact]
    public void Generates_Extension_Property_For_Controller()
    {
        var generated = Run();

        Assert.Contains("public HomeControllerLinkGenerator Home", generated);
        Assert.Contains("=> new HomeControllerLinkGenerator(linkGenerator);", generated);
    }

    [Fact]
    public void Generates_LinkGenerator_Call()
    {
        var generated = Run();

        Assert.Contains("linkGenerator.GetPathByAction", generated);
        Assert.Contains("action: \"Index\"", generated);
        Assert.Contains("controller: \"Home\"", generated);
    }

    private static string Run()
    {
        var generator = new LinkGeneratorGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(ControllerSource)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Controller).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(LinkGenerator).Assembly.Location)
            ]);

        var result = driver.RunGenerators(compilation).GetRunResult();

        return result.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("LinkGeneratorExtensions.g.cs"))
            .GetText()
            .ToString();
    }
}
