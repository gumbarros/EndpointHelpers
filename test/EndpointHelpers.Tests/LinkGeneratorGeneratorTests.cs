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

    private const string AreaControllerSource = """

                                                using Microsoft.AspNetCore.Mvc;

                                                namespace EndpointHelpers
                                                {
                                                    [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class | System.AttributeTargets.Assembly)]
                                                    public sealed class GenerateLinkGeneratorAttribute : System.Attribute;
                                                }

                                                namespace Test
                                                {
                                                    using EndpointHelpers;

                                                    [Area("Admin")]
                                                    public class UsersController : Controller
                                                    {
                                                        [GenerateLinkGenerator]
                                                        public IActionResult Index(int id) => Ok();
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

    [Fact]
    public void Generates_Area_Nested_Extensions()
    {
        var generated = Run(AreaControllerSource);

        Assert.Contains("public sealed class AdminAreaLinkGenerator(LinkGenerator linkGenerator)", generated);
        Assert.Contains("public UsersControllerLinkGenerator Users", generated);
        Assert.Contains("public AdminAreaLinkGenerator Admin", generated);
    }

    [Fact]
    public void Includes_Area_In_LinkGenerator_Values()
    {
        var generated = Run(AreaControllerSource);

        Assert.Contains("area = \"Admin\"", generated);
    }

    private static string Run()
        => Run(ControllerSource);

    private static string Run(string source)
    {
        var generator = new LinkGeneratorGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Controller).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(AreaAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(LinkGenerator).Assembly.Location)
            ]);

        var result = driver.RunGenerators(compilation).GetRunResult();

        return result.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("LinkGeneratorExtensions.g.cs"))
            .GetText()
            .ToString();
    }
}
