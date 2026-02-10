using System.ComponentModel.DataAnnotations;

namespace EndpointHelpers.Sample.Models;

public sealed class OrderEditModel
{
    [Required]
    public string Customer { get; set; } = "";

    [Required]
    public string Status { get; set; } = "Open";

    [Range(0.01, 1000000)]
    public decimal Total { get; set; }
}
