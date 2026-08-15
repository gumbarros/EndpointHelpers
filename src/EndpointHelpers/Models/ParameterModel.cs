namespace EndpointHelpers.Models;

public sealed record ParameterModel
{
    public required string TypeName { get; init; }
    public required string Name { get; init; }
    public required bool IsOptional { get; init; }
    public required string? DefaultValueLiteral { get; init; }
}