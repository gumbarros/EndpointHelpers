namespace EndpointHelpers.Sample.Models;

public sealed class Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime CreatedUtc { get; set; }
}
