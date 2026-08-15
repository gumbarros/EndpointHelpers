using System.Collections.Immutable;

namespace EndpointHelpers.Models;

public sealed record ControllerModel
{
    public required string MetadataName { get; init; }
    public required string TypeName { get; init; }
    public required string NamespaceName { get; init; }
    public required string Name { get; init; }
    public required string AccessibilityKeyword { get; init; }
    public required bool IsSealed { get; init; }
    public required string? AreaName { get; init; }
    public required bool ClassHasGenerateAttribute { get; init; }
    public required ImmutableArray<ActionModel> Methods { get; init; }
}