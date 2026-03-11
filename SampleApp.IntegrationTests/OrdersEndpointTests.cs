using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<OrderResponse>();
        payload.Should().NotBeNull();
        payload!.ProductCode.Should().Be("PEN");
        payload.Quantity.Should().Be(3);
        payload.UnitPrice.Should().Be(12.50m);
        payload.TotalPrice.Should().Be(37.50m);

        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedOrder = await dbContext.Orders.FindAsync(payload.Id);

        savedOrder.Should().NotBeNull();
        savedOrder!.ProductCode.Should().Be("PEN");
        savedOrder.Quantity.Should().Be(3);
        savedOrder.UnitPrice.Should().Be(12.50m);

        fixture.CountRequests("/products/PEN").Should().Be(1);
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenCatalogProductIsMissing()
    {
        await fixture.ResetStateAsync();
        fixture.SetupMissingProduct("UNKNOWN");

        var response = await fixture.Client.PostAsJsonAsync("/orders", new CreateOrderRequest("UNKNOWN", 2));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Orders.Should().BeEmpty();

        fixture.CountRequests("/products/UNKNOWN").Should().Be(1);
    }
}
