using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EndpointHelpers.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace EndpointHelpers.Helpers;

internal static class IdentifierHelpers
{
    public static string TrimControllerSuffix(string name)
    {
        return name.EndsWith("Controller", StringComparison.Ordinal)
            ? name[..^"Controller".Length]
            : name;
    }

    public static string GetUniqueControllerIdentifier(
        ControllerModel controller,
        IReadOnlyList<ControllerModel> controllers)
    {
        var sameNameCount = controllers.Count(other =>
            string.Equals(other.Name, controller.Name, StringComparison.Ordinal));

        return sameNameCount == 1
            ? controller.Name
            : ToIdentifier(controller.MetadataName);
    }

    public static string EscapeStringLiteral(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    public static string ToIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "_";

        var sb = new StringBuilder(value.Length + 1);

        if (!IsIdentifierStart(value[0]))
            sb.Append('_');

        foreach (var c in value)
            sb.Append(IsIdentifierPart(c) ? c : '_');

        var identifier = sb.Length == 0 ? "_" : sb.ToString();
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : "@" + identifier;
    }

    private static bool IsIdentifierStart(char c) => c == '_' || char.IsLetter(c);

    private static bool IsIdentifierPart(char c) => c == '_' || char.IsLetterOrDigit(c);
}
