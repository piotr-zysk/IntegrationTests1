namespace SampleApp.Models;

public sealed class Order
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
