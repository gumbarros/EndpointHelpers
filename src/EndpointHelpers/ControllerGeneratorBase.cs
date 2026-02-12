using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EndpointHelpers;

public abstract class ControllerGeneratorBase : IIncrementalGenerator
{
    protected const string EndpointHelpersNamespace = "EndpointHelpers";

    private const string UnifiedGenerateAttributeName = "GenerateEndpointHelpersAttribute";
    private const string NonActionAttributeName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";

    protected abstract string GenerateAttributeName { get; }
    protected abstract string IgnoreAttributeName { get; }
    protected abstract string OutputFileName { get; }

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

        context.RegisterSourceOutput(
            controllers.Collect().Combine(assemblyHasGenerate),
            (ctx, data) => Generate(ctx, data.Left, data.Right));
    }

    protected virtual void RegisterPostInitialization(IncrementalGeneratorInitializationContext context)
    {
    }

    protected abstract string BuildSource(IReadOnlyList<ControllerModel> selectedControllers);

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

        return attributeName == $"{EndpointHelpersNamespace}.{GenerateAttributeName}" ||
               attributeName == $"{EndpointHelpersNamespace}.{UnifiedGenerateAttributeName}";
    }

    private ControllerModel? TransformController(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not { } typeSymbol)
            return null;

        var classHasGenerateAttribute = HasAnyAttribute(
            typeSymbol,
            $"{EndpointHelpersNamespace}.{GenerateAttributeName}",
            $"{EndpointHelpersNamespace}.{UnifiedGenerateAttributeName}");

        var methods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method =>
                method.MethodKind == MethodKind.Ordinary &&
                method.DeclaredAccessibility == Accessibility.Public)
            .Where(method => !HasAnyAttribute(method, $"{EndpointHelpersNamespace}.{IgnoreAttributeName}"))
            .Where(static method => !HasAnyAttribute(method, NonActionAttributeName))
            .Select(method => new ActionModel(
                method.Name,
                HasAnyAttribute(
                    method,
                    $"{EndpointHelpersNamespace}.{GenerateAttributeName}",
                    $"{EndpointHelpersNamespace}.{UnifiedGenerateAttributeName}"),
                [
                    ..method.Parameters
                        .Where(static parameter => !IsIgnoredParameter(parameter))
                        .Select(static parameter => new ParameterModel(
                            parameter.Type.ToDisplayString(),
                            parameter.Name,
                            parameter.IsOptional,
                            GetOptionalDefaultLiteral(parameter)))
                ]))
            .ToImmutableArray();

        return new ControllerModel(
            GetMetadataName(typeSymbol),
            typeSymbol.ToDisplayString(),
            typeSymbol.Name,
            classHasGenerateAttribute,
            methods);
    }

    private void Generate(
        SourceProductionContext context,
        IReadOnlyList<ControllerModel> rawControllers,
        bool assemblyHasGenerate)
    {
        var selectedControllers = SelectControllers(rawControllers, assemblyHasGenerate);
        if (selectedControllers.Length == 0)
            return;

        context.AddSource(
            OutputFileName,
            SourceText.From(BuildSource(selectedControllers), Encoding.UTF8));
    }

    private static ImmutableArray<ControllerModel> SelectControllers(
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
                .Select(controller => controller with
                {
                    Methods =
                    [
                        ..controller.Methods
                            .Where(method =>
                                assemblyHasGenerate || controller.ClassHasGenerateAttribute ||
                                method.HasGenerateAttribute)
                    ]
                })
                .Where(static controller => controller.Methods.Length > 0)
        ];
    }

    private static bool HasAnyAttribute(ISymbol symbol, params string[] fullMetadataNames)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name is null)
                continue;

            if (fullMetadataNames.Any(metadataName => name == metadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetOptionalDefaultLiteral(IParameterSymbol parameter)
    {
        if (!parameter.IsOptional)
            return string.Empty;

        if (!parameter.HasExplicitDefaultValue)
            return parameter.Type.IsValueType ? "default" : "null";

        var value = parameter.ExplicitDefaultValue;

        if (value is null)
            return "null";

        if (parameter.Type.TypeKind == TypeKind.Enum)
        {
            var enumType = (INamedTypeSymbol)parameter.Type;

            foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!member.HasConstantValue)
                    continue;

                if (Equals(member.ConstantValue, value))
                    return $"{enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{member.Name}";
            }

            var underlying = SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: true);
            return $"({enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){underlying}";
        }

        return SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: true);
    }


    private static bool IsIgnoredParameter(IParameterSymbol parameter)
    {
        if (parameter.Type is INamedTypeSymbol { Name: "CancellationToken" } namedType &&
            namedType.ContainingNamespace?.ToDisplayString() == "System.Threading")
        {
            return true;
        }

        return HasAnyAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromServicesAttribute");
    }

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        var current = type.ContainingType;

        while (current is not null)
        {
            name = $"{current.MetadataName}+{name}";
            current = current.ContainingType;
        }

        var ns = type.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    protected sealed record ControllerModel(
        string MetadataName,
        string TypeName,
        string Name,
        bool ClassHasGenerateAttribute,
        ImmutableArray<ActionModel> Methods);

    protected sealed record ActionModel(
        string Name,
        bool HasGenerateAttribute,
        ImmutableArray<ParameterModel> Parameters);

    protected sealed record ParameterModel(
        string TypeName,
        string Name,
        bool IsOptional,
        string? DefaultValueLiteral);
}