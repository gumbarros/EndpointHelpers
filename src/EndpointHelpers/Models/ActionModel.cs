using System.Collections.Immutable;

namespace EndpointHelpers.Models;

public sealed record ActionModel
{
    public required string Name { get; init; }
    public required bool HasGenerateAttribute { get; init; }
    public required ImmutableArray<ParameterModel> Parameters { get; init; }
}