using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EndpointHelpers.Helpers;

internal static class ParameterHelpers
{
    public static bool IsIgnoredParameter(IParameterSymbol parameter)
    {
        if (parameter.Type is INamedTypeSymbol { Name: "CancellationToken" } namedType &&
            namedType.ContainingNamespace?.ToDisplayString() == "System.Threading")
        {
            return true;
        }

        return SymbolHelpers.HasAnyAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromServicesAttribute");
    }

    public static bool IsRoutableParameter(IParameterSymbol parameter)
    {
        if (SymbolHelpers.HasAnyAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromBodyAttribute"))
            return false;

        if (SymbolHelpers.HasAnyAttribute(
                parameter,
                "Microsoft.AspNetCore.Mvc.FromRouteAttribute",
                "Microsoft.AspNetCore.Mvc.FromQueryAttribute",
                "Microsoft.AspNetCore.Mvc.FromHeaderAttribute",
                "Microsoft.AspNetCore.Mvc.FromFormAttribute"))
        {
            return true;
        }

        return IsSimpleType(parameter.Type);
    }

    public static string? GetOptionalDefaultLiteral(IParameterSymbol parameter)
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

    private static bool IsSimpleType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
        {
            type = named.TypeArguments[0];
        }

        if (type.TypeKind == TypeKind.Enum)
            return true;

        return type.SpecialType switch
        {
            SpecialType.System_Boolean or
            SpecialType.System_Char or
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Decimal or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_String or
            SpecialType.System_IntPtr or
            SpecialType.System_UIntPtr => true,
            _ => IsKnownSimpleSystemType(type)
        };
    }

    private static bool IsKnownSimpleSystemType(ITypeSymbol type)
    {
        if (type.ContainingNamespace?.ToDisplayString() != "System")
            return false;

        return type.Name is
            "Guid" or "DateTime" or "DateTimeOffset" or "TimeSpan" or
            "DateOnly" or "TimeOnly" or "Uri" or "Version" or
            "BigInteger" or "Half" or "Int128" or "UInt128";
    }
}
