using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleApp.Data;
using SampleApp.Models;

namespace SampleApp.IntegrationTests;

public sealed class OrdersEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        await factory.CreateDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateOrder_ShouldPersistOrderAndCallCatalog()
    {
        const string productCode = "PEN";
        await factory.ResetStateAsync();
        factory.SetupProduct(productCode, 12.50m);

        var response = await _client.PostAsJsonAsync("/orders", new CreateOrderRequest(productCode, 3));
        // var body = await response.Content.ReadAsStringAsync();
        // Console.WriteLine($"CreateOrder success test response: {response.StatusCode} - {body}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(payload);
        Assert.Equal("PEN", payload!.ProductCode);
        Assert.Equal(3, payload.Quantity);
        Assert.Equal(12.50m, payload.UnitPrice);
        Assert.Equal(37.50m, payload.TotalPrice);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedOrder = await dbContext.Orders.FindAsync(payload.Id);

        Assert.NotNull(savedOrder);
        Assert.Equal("PEN", savedOrder!.ProductCode);
        Assert.Equal(3, savedOrder.Quantity);
        Assert.Equal(12.50m, savedOrder.UnitPrice);

        Assert.Equal(1, factory.CountRequests("/products/PEN"));
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenCatalogProductIsMissing()
    {
        const string productCode = "UNKNOWN";
        await factory.ResetStateAsync();
        factory.SetupMissingProduct(productCode);

        var response = await _client.PostAsJsonAsync("/orders", new CreateOrderRequest(productCode, 2));
        // var body = await response.Content.ReadAsStringAsync();
        // Console.WriteLine($"CreateOrder bad request test response: {response.StatusCode} - {body}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(dbContext.Orders);

        Assert.Equal(1, factory.CountRequests("/products/UNKNOWN"));
    }
    
    [Fact]
    public async Task GetRoot_WithRealHost_ShouldReturnAStatusCode()
    {
        var response = await _client.GetAsync("/");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound);
    }
}
