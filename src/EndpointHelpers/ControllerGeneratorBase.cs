using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using EndpointHelpers.Helpers;
using EndpointHelpers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EndpointHelpers;

public abstract class ControllerGeneratorBase : IIncrementalGenerator
{
    protected const string EndpointHelpersNamespace = "EndpointHelpers";

    private const string NonActionAttributeName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";

    protected abstract string GenerateAttributeName { get; }
    protected abstract string IgnoreAttributeName { get; }
    protected abstract string AttributeFileName { get; }
    protected abstract string AttributeResourceName { get; }
    protected virtual bool SupportsAssemblyGenerateAttribute => true;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        RegisterPostInitialization(context);

        var assemblyHasGenerate = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is AttributeSyntax attribute && IsAssemblyAttributeCandidate(attribute),
                (ctx, _) => IsAssemblyGenerateAttribute(ctx))
            .Where(static match => match)
            .Collect()
            .Select(static (matches, _) => matches.Length > 0);

        var controllers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                (ctx, _) => TransformController(ctx))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        RegisterSourceOutput(context, controllers, assemblyHasGenerate);
    }

    private void RegisterPostInitialization(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddEmbeddedAttributeDefinition();
            ctx.AddSource(
                AttributeFileName,
                SourceText.From(ReadEmbeddedResource(AttributeResourceName), Encoding.UTF8));
        });
    }

    private static string ReadEmbeddedResource(string name)
    {
        using var stream = typeof(ControllerGeneratorBase).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    protected virtual void RegisterSourceOutput(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<ControllerModel> controllers,
        IncrementalValueProvider<bool> assemblyHasGenerate)
    {
        context.RegisterSourceOutput(
            controllers.Collect().Combine(assemblyHasGenerate),
            (ctx, data) => Generate(ctx, data.Left, data.Right));
    }

    protected abstract IEnumerable<GeneratedFile> BuildSources(
        ImmutableArray<ControllerModel> selectedControllers,
        bool hasJetbrainsAnnotations = false);

    private static bool IsAssemblyAttributeCandidate(AttributeSyntax attribute)
    {
        return attribute.Parent is AttributeListSyntax
        {
            Target.Identifier.ValueText: "assembly"
        };
    }

    private bool IsAssemblyGenerateAttribute(GeneratorSyntaxContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
        var attributeName = symbol?.ContainingType.ToDisplayString();

        return attributeName == $"{EndpointHelpersNamespace}.{GenerateAttributeName}";
    }

    private ControllerModel? TransformController(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
            return null;

        var classHasGenerateAttribute = SymbolHelpers.HasAnyAttribute(
            typeSymbol,
            $"{EndpointHelpersNamespace}.{GenerateAttributeName}");

        var methods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method =>
                method.MethodKind == MethodKind.Ordinary &&
                method.DeclaredAccessibility == Accessibility.Public)
            .Where(method => !SymbolHelpers.HasAnyAttribute(method, $"{EndpointHelpersNamespace}.{IgnoreAttributeName}"))
            .Where(static method => !SymbolHelpers.HasAnyAttribute(method, NonActionAttributeName))
            .Select(method => new ActionModel
            {
                Name = method.Name,
                HasGenerateAttribute = SymbolHelpers.HasAnyAttribute(
                    method,
                    $"{EndpointHelpersNamespace}.{GenerateAttributeName}"),
                Parameters =
                [
                    ..method.Parameters
                        .Where(static parameter => !ParameterHelpers.IsIgnoredParameter(parameter))
                        .Where(static parameter => ParameterHelpers.IsRoutableParameter(parameter))
                        .Select(static parameter => new ParameterModel
                        {
                            TypeName = parameter.Type.ToDisplayString(),
                            Name = parameter.Name,
                            IsOptional = parameter.IsOptional,
                            DefaultValueLiteral = ParameterHelpers.GetOptionalDefaultLiteral(parameter)
                        })
                ]
            })
            .GroupBy(
                static method => method.Name + "(" + string.Join(",", method.Parameters.Select(static parameter => parameter.TypeName)) + ")",
                StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToImmutableArray();

        var areaName = SymbolHelpers.GetAreaName(typeSymbol) ?? SymbolHelpers.GetAreaName(classDeclaration, context.SemanticModel);

        return new ControllerModel
        {
            MetadataName = SymbolHelpers.GetMetadataName(typeSymbol),
            TypeName = typeSymbol.ToDisplayString(),
            NamespaceName = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            Name = typeSymbol.Name,
            AccessibilityKeyword = SymbolHelpers.GetAccessibilityKeyword(typeSymbol.DeclaredAccessibility),
            IsSealed = typeSymbol.IsSealed,
            AreaName = areaName,
            ClassHasGenerateAttribute = classHasGenerateAttribute,
            Methods = methods
        };
    }

    protected void Generate(
        SourceProductionContext context,
        IReadOnlyList<ControllerModel> rawControllers,
        bool assemblyHasGenerate,
        bool hasJetbrainsAnnotations = false)
    {
        var effectiveAssemblyHasGenerate = SupportsAssemblyGenerateAttribute && assemblyHasGenerate;
        var selectedControllers = SelectControllers(rawControllers, effectiveAssemblyHasGenerate);
        if (selectedControllers.Length == 0)
            return;

        foreach (var file in BuildSources(selectedControllers, hasJetbrainsAnnotations))
            context.AddSource(file.FileName, SourceText.From(file.Content, Encoding.UTF8));
    }

    protected static ImmutableArray<ControllerModel> SelectControllers(
        IReadOnlyList<ControllerModel> rawControllers,
        bool assemblyHasGenerate)
    {
        var controllers = rawControllers
            .OrderBy(static controller => controller.MetadataName, StringComparer.Ordinal)
            .GroupBy(static controller => controller.MetadataName, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();

        return
        [
            ..controllers
                .Where(controller => assemblyHasGenerate
                    ? controller.Name.EndsWith("Controller", StringComparison.Ordinal)
                    : controller.ClassHasGenerateAttribute ||
                      controller.Methods.Any(static method => method.HasGenerateAttribute))
                .Select(controller =>
                {
                    ImmutableArray<ActionModel> methods = [
                        ..controller.Methods
                            .Where(method =>
                                assemblyHasGenerate || controller.ClassHasGenerateAttribute ||
                                method.HasGenerateAttribute)
                    ];
                    return controller with { Methods = methods };
                })
                .Where(static controller => controller.Methods.Length > 0)
        ];
    }

    protected static string GetNamespaceFileName(string outputFileName, string? namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
            return outputFileName;

        const string suffix = ".g.cs";
        return outputFileName.EndsWith(suffix, StringComparison.Ordinal)
            ? $"{outputFileName[..^suffix.Length]}.{namespaceName}{suffix}"
            : $"{outputFileName}.{namespaceName}";
    }

    protected readonly record struct GeneratedFile(string FileName, string Content);
}
