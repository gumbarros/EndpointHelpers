using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EndpointHelpers.Helpers;

internal static class SymbolHelpers
{
    public static bool HasAnyAttribute(ISymbol symbol, params string[] fullMetadataNames)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name is null)
                continue;

            if (fullMetadataNames.Any(metadataName => name == metadataName))
                return true;
        }

        return false;
    }

    public static string? GetAreaName(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAreaAttribute(attribute.AttributeClass))
                continue;

            if (TryGetAreaValue(attribute) is { } value &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    public static string? GetAreaName(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        foreach (var attribute in classDeclaration.AttributeLists.SelectMany(static list => list.Attributes))
        {
            if (semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol symbol)
            {
                if (!IsAreaAttribute(symbol.ContainingType))
                    continue;
            }
            else
            {
                var simpleName = GetAttributeSimpleName(attribute.Name);
                if (simpleName is not ("Area" or "AreaAttribute"))
                    continue;
            }

            var args = attribute.ArgumentList?.Arguments;
            if (args is null || args.Value.Count == 0)
                continue;

            foreach (var argument in args.Value)
            {
                if (TryGetStringConstant(semanticModel, argument.Expression) is not { } value)
                    continue;

                if (argument.NameEquals is null &&
                    argument.NameColon is null &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                if (argument.NameEquals?.Name.Identifier.ValueText == "RouteValue" &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    public static string GetMetadataName(INamedTypeSymbol type)
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

    public static string GetAccessibilityKeyword(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal"
        };
    }

    private static bool IsAreaAttribute(INamedTypeSymbol? attributeClass)
    {
        return attributeClass is
        {
            Name: "AreaAttribute",
            ContainingNamespace: { }
        } && attributeClass.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Mvc";
    }

    private static string? TryGetAreaValue(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string constructorValue)
        {
            return constructorValue;
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument is { Key: "RouteValue", Value.Value: string routeValue })
            {
                return routeValue;
            }
        }

        return null;
    }

    private static string? GetAttributeSimpleName(NameSyntax nameSyntax)
    {
        return nameSyntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static string? TryGetStringConstant(SemanticModel semanticModel, ExpressionSyntax expression)
    {
        if (semanticModel.GetConstantValue(expression) is { HasValue: true, Value: string value })
            return value;

        return expression is LiteralExpressionSyntax literal &&
               literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }
}
