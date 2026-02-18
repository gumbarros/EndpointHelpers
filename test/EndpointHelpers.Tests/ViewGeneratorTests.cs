using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EndpointHelpers.Tests;

public sealed class ViewGeneratorTests
{
    private const string JetBrainsAnnotationsSource = """

                                                      namespace JetBrains.Annotations
                                                      {
                                                          [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Parameter)]
                                                          public sealed class AspMvcViewAttribute : System.Attribute;

                                                          [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Parameter)]
                                                          public sealed class AspMvcActionAttribute : System.Attribute;

                                                          [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Parameter)]
                                                          public sealed class AspMvcModelTypeAttribute : System.Attribute;
                                                      }
                                                      """;

    private const string ControllerSource = """

                                            using Microsoft.AspNetCore.Mvc;

                                            namespace EndpointHelpers
                                            {
                                                [System.AttributeUsage(System.AttributeTargets.Class)]
                                                public sealed class GenerateViewHelpersAttribute : System.Attribute;

                                                [System.AttributeUsage(System.AttributeTargets.Method)]
                                                public sealed class ViewHelpersIgnoreAttribute : System.Attribute;
                                            }

                                            namespace Test
                                            {
                                                using EndpointHelpers;

                                                [GenerateViewHelpers]
                                                public partial class HomeController : Controller
                                                {
                                                    public IActionResult Index() => View();
                                                    public IActionResult Grid() => View();
                                                }

                                                // Razor view generated types (simplified for test)
                                                public class Views_Home_Index : RazorPage
                                                {
                                                    public override System.Threading.Tasks.Task ExecuteAsync() => System.Threading.Tasks.Task.CompletedTask;
                                                }

                                                public class Views_Home_Grid : RazorPage
                                                {
                                                    public override System.Threading.Tasks.Task ExecuteAsync() => System.Threading.Tasks.Task.CompletedTask;
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
    public void Generates_View_Helper_Methods_For_Action()
    {
        var generated = Run();

        Assert.Contains("protected ViewResult IndexView()", generated);
        Assert.Contains("ViewName = \"Index\"", generated);
    }

    [Fact]
    public void Generates_Only_Default_And_Model_View_Overloads()
    {
        var generated = Run();

        Assert.Contains("protected ViewResult IndexView()", generated);
        Assert.Contains("protected ViewResult IndexView(object? model)", generated);
        Assert.DoesNotContain("protected ViewResult IndexView(string? viewName)", generated);
        Assert.DoesNotContain("protected ViewResult IndexView(string? viewName, object? model)", generated);
    }

    [Fact]
    public void Generates_All_Views_Found()
    {
        var generated = Run();

        Assert.Contains("IndexView()", generated);
        Assert.Contains("GridView()", generated);
    }

    [Fact]
    public void Applies_ModelType_Attribute_To_Model_Parameter()
    {
        var generated = Run($"{JetBrainsAnnotationsSource}\n{ControllerSource}");

        Assert.Contains(
            "protected ViewResult IndexView([global::JetBrains.Annotations.AspMvcModelTypeAttribute] object? model)",
            generated);
    }

    private static string Run(string? source = null)
    {
        var generator = new ViewGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(source ?? ControllerSource)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Controller).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ViewResult).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RazorPage).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ITempDataProvider).Assembly.Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = driver.RunGenerators(compilation).GetRunResult();

        return result.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("ViewHelpers.g.cs"))
            .GetText()
            .ToString();
    }
}
