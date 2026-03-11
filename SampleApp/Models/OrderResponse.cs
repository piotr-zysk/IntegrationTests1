namespace SampleApp.Models;

public sealed record OrderResponse(
    int Id,
    string ProductCode,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    DateTime CreatedAtUtc);
