using Microsoft.EntityFrameworkCore;
using SampleApp.Clients;
using SampleApp.Data;
using SampleApp.Models;

namespace SampleApp;

public static class AppComposition
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SqlServer.");
        var catalogBaseUrl = configuration["Catalog:BaseUrl"]
            ?? throw new InvalidOperationException("Missing Catalog:BaseUrl.");

        services.AddOpenApi();
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        services
            .AddHttpClient<IProductCatalogClient, ProductCatalogClient>(client =>
            {
                client.BaseAddress = new Uri(catalogBaseUrl);
            });
    }

    public static void ConfigureEndpoints(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapPost("/orders", CreateOrderAsync);
        app.MapGet("/orders/{id:int}", GetOrderAsync);
    }

    private static async Task<IResult> CreateOrderAsync(
        CreateOrderRequest request,
        AppDbContext dbContext,
        IProductCatalogClient catalogClient,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return Results.BadRequest("Quantity must be greater than zero.");
        }

        var product = await catalogClient.GetProductAsync(request.ProductCode, cancellationToken);
        if (product is null)
        {
            return Results.BadRequest($"Product '{request.ProductCode}' was not found in catalog.");
        }

        var order = new Order
        {
            ProductCode = product.Code,
            Quantity = request.Quantity,
            UnitPrice = product.UnitPrice,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/orders/{order.Id}", new OrderResponse(
            order.Id,
            order.ProductCode,
            order.Quantity,
            order.UnitPrice,
            order.Quantity * order.UnitPrice,
            order.CreatedAtUtc));
    }

    private static async Task<IResult> GetOrderAsync(int id, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order is null
            ? Results.NotFound()
            : Results.Ok(new OrderResponse(
                order.Id,
                order.ProductCode,
                order.Quantity,
                order.UnitPrice,
                order.Quantity * order.UnitPrice,
                order.CreatedAtUtc));
    }
}
