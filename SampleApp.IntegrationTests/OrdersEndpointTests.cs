using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleApp.Data;
using SampleApp.Models;

namespace SampleApp.IntegrationTests;

public sealed class OrdersEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task CreateOrder_ShouldPersistOrderAndCallCatalog()
    {
        await fixture.ResetStateAsync();
        fixture.SetupProduct("PEN", 12.50m);

        var response = await fixture.Client.PostAsJsonAsync("/orders", new CreateOrderRequest("PEN", 3));
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"CreateOrder success test response: {response.StatusCode} - {body}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(payload);
        Assert.Equal("PEN", payload!.ProductCode);
        Assert.Equal(3, payload.Quantity);
        Assert.Equal(12.50m, payload.UnitPrice);
        Assert.Equal(37.50m, payload.TotalPrice);

        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedOrder = await dbContext.Orders.FindAsync(payload.Id);

        Assert.NotNull(savedOrder);
        Assert.Equal("PEN", savedOrder!.ProductCode);
        Assert.Equal(3, savedOrder.Quantity);
        Assert.Equal(12.50m, savedOrder.UnitPrice);

        Assert.Equal(1, fixture.CountRequests("/products/PEN"));
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenCatalogProductIsMissing()
    {
        await fixture.ResetStateAsync();
        fixture.SetupMissingProduct("UNKNOWN");

        var response = await fixture.Client.PostAsJsonAsync("/orders", new CreateOrderRequest("UNKNOWN", 2));
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"CreateOrder bad request test response: {response.StatusCode} - {body}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(dbContext.Orders);

        Assert.Equal(1, fixture.CountRequests("/products/UNKNOWN"));
    }
}
