using System;
using System.Collections.Immutable;
using System.Globalization;
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

    protected abstract string BuildSource(ImmutableArray<ControllerModel> selectedControllers);

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
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
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
                method.Parameters.Select(static parameter => new ParameterModel(
                    parameter.Type.ToDisplayString(),
                    parameter.Name,
                    parameter.IsOptional,
                    GetOptionalDefaultLiteral(parameter))).ToImmutableArray()))
            .ToImmutableArray();

        return new ControllerModel(
            GetMetadataName(typeSymbol),
            typeSymbol.ToDisplayString(),
            typeSymbol.Name,
            classHasGenerateAttribute,
            methods);
    }

    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<ControllerModel> rawControllers,
        bool assemblyHasGenerate,
        ControllerGeneratorBase generator)
    {
        var selectedControllers = SelectControllers(rawControllers, assemblyHasGenerate);
        if (selectedControllers.Length == 0)
            return;

        context.AddSource(
            generator.OutputFileName,
            SourceText.From(generator.BuildSource(selectedControllers), Encoding.UTF8));
    }

    private void Generate(
        SourceProductionContext context,
        ImmutableArray<ControllerModel> rawControllers,
        bool assemblyHasGenerate)
    {
        Generate(context, rawControllers, assemblyHasGenerate, this);
    }

    private static ImmutableArray<ControllerModel> SelectControllers(
        ImmutableArray<ControllerModel> rawControllers,
        bool assemblyHasGenerate)
    {
        var controllers = rawControllers
            .OrderBy(static controller => controller.MetadataName, StringComparer.Ordinal)
            .GroupBy(static controller => controller.MetadataName, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();

        return controllers
            .Where(controller => assemblyHasGenerate
                ? controller.Name.EndsWith("Controller", StringComparison.Ordinal)
                : controller.ClassHasGenerateAttribute || controller.Methods.Any(static method => method.HasGenerateAttribute))
            .Select(controller => new ControllerModel(
                controller.MetadataName,
                controller.TypeName,
                controller.Name,
                controller.ClassHasGenerateAttribute,
                controller.Methods
                    .Where(method => assemblyHasGenerate || controller.ClassHasGenerateAttribute || method.HasGenerateAttribute)
                    .ToImmutableArray()))
            .Where(static controller => controller.Methods.Length > 0)
            .ToImmutableArray();
    }

    protected static bool HasAnyAttribute(ISymbol symbol, params string[] fullMetadataNames)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name is null)
                continue;

            foreach (var metadataName in fullMetadataNames)
            {
                if (name == metadataName)
                    return true;
            }
        }

        return false;
    }

    private static string GetOptionalDefaultLiteral(IParameterSymbol parameter)
    {
        if (!parameter.IsOptional)
            return string.Empty;

        if (!parameter.HasExplicitDefaultValue || parameter.ExplicitDefaultValue is null)
            return "null";

        var value = parameter.ExplicitDefaultValue;
        return value switch
        {
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            bool b => b ? "true" : "false",
            float f => f.ToString("R", CultureInfo.InvariantCulture) + "F",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "M",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null"
        };
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

    protected sealed class ControllerModel
    {
        public ControllerModel(
            string metadataName,
            string typeName,
            string name,
            bool classHasGenerateAttribute,
            ImmutableArray<ActionModel> methods)
        {
            MetadataName = metadataName;
            TypeName = typeName;
            Name = name;
            ClassHasGenerateAttribute = classHasGenerateAttribute;
            Methods = methods;
        }

        public string MetadataName { get; }
        public string TypeName { get; }
        public string Name { get; }
        public bool ClassHasGenerateAttribute { get; }
        public ImmutableArray<ActionModel> Methods { get; }
    }

    protected sealed class ActionModel
    {
        public ActionModel(
            string name,
            bool hasGenerateAttribute,
            ImmutableArray<ParameterModel> parameters)
        {
            Name = name;
            HasGenerateAttribute = hasGenerateAttribute;
            Parameters = parameters;
        }

        public string Name { get; }
        public bool HasGenerateAttribute { get; }
        public ImmutableArray<ParameterModel> Parameters { get; }
    }

    protected sealed class ParameterModel
    {
        public ParameterModel(
            string typeName,
            string name,
            bool isOptional,
            string defaultValueLiteral)
        {
            TypeName = typeName;
            Name = name;
            IsOptional = isOptional;
            DefaultValueLiteral = defaultValueLiteral;
        }

        public string TypeName { get; }
        public string Name { get; }
        public bool IsOptional { get; }
        public string DefaultValueLiteral { get; }
    }
}
