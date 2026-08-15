using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EndpointHelpers.Tests;

public sealed class GeneratedCodeCompilationTests
{
    private static readonly MetadataReference[] References = BuildReferences();

    private const string ControllerSource = """

                                            using System;
                                            using Microsoft.AspNetCore.Mvc;
                                            using Microsoft.AspNetCore.Mvc.Razor;
                                            using EndpointHelpers;

                                            namespace Test;

                                            public class OrderModel
                                            {
                                                public int Id { get; set; }
                                            }

                                            [GenerateUrlHelper]
                                            [GenerateLinkGenerator]
                                            [GenerateRedirectToAction]
                                            [GenerateViewHelpers]
                                            public partial class HomeController : Controller
                                            {
                                                public IActionResult Index(int id, string slug) => Ok();
                                                public IActionResult Create() => Ok();
                                                public IActionResult Create(OrderModel model) => Ok();
                                                public IActionResult Edit(int id) => Ok();
                                                public IActionResult Edit(int id, OrderModel model) => Ok();
                                            }

                                            [Area("Admin")]
                                            [GenerateUrlHelper]
                                            [GenerateLinkGenerator]
                                            public class UsersController : Controller
                                            {
                                                public IActionResult Index(int id) => Ok();
                                            }

                                            public class Views_Home_Index : RazorPage
                                            {
                                                public override System.Threading.Tasks.Task ExecuteAsync() => System.Threading.Tasks.Task.CompletedTask;
                                            }
                                            """;

    [Fact]
    public void Generated_Code_Compiles_Without_Errors()
    {
        var generators = new ControllerGeneratorBase[]
        {
            new UrlHelperGenerator(),
            new LinkGeneratorGenerator(),
            new RedirectToActionGenerator(),
            new ViewGenerator()
        };

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(ControllerSource, parseOptions);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var inputCompilation = CSharpCompilation.Create("Input", [inputTree], References, options);

        var driver = CSharpGeneratorDriver.Create(
            generators.Select(static generator => generator.AsSourceGenerator()),
            parseOptions: parseOptions);
        var runResult = driver.RunGenerators(inputCompilation).GetRunResult();

        var generatedTrees = runResult.GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.ToString(), parseOptions));

        var finalCompilation = CSharpCompilation.Create(
            "Output",
            new[] { inputTree }.Concat(generatedTrees),
            References,
            options);

        var errors = finalCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            "Generated code failed to compile:\n" + string.Join("\n", errors));
    }

    private static MetadataReference[] BuildReferences()
    {
        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?
            .Split(Path.PathSeparator)
            ?? [];

        return trustedAssemblies
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static MetadataReference (path) => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
