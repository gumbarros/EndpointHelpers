namespace EndpointHelpers.Sample.Models;

public sealed class Product
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public double Rating { get; init; }
    public bool Featured { get; init; }
    public string Summary { get; init; } = string.Empty;
}
