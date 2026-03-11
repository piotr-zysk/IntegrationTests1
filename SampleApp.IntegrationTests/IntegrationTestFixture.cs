using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SampleApp.Data;
using Testcontainers.MsSql;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SampleApp.IntegrationTests;

public sealed class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly string _databaseName = $"SampleAppTests_{Guid.NewGuid():N}";
    private readonly WireMockServer _wireMockServer = WireMockServer.Start();

    public HttpClient Client => CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var configurationOverrides = new Dictionary<string, string?>
            {
                ["Catalog:BaseUrl"] = _wireMockServer.Url!
            };

            configBuilder.AddInMemoryCollection(configurationOverrides);
        });

        builder.ConfigureServices(services =>
        {
            // Replace the AppDbContext registration to point at the SQL Server testcontainer.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(BuildConnectionString());
            });
        });
    }

    public IntegrationTestFixture()
    {
        // Ensure the SQL Server testcontainer is started before the host builds,
        // so that the overridden connection string points to a live database.
        _sqlContainer.StartAsync().GetAwaiter().GetResult();
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _wireMockServer.Stop();
        _wireMockServer.Dispose();
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }

    public void SetupProduct(string code, decimal unitPrice)
    {
        _wireMockServer
            .Given(Request.Create().WithPath($"/products/{code}").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(new { code, unitPrice }));
    }

    public void SetupMissingProduct(string code)
    {
        _wireMockServer
            .Given(Request.Create().WithPath($"/products/{code}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));
    }

    public int CountRequests(string path)
    {
        return _wireMockServer.LogEntries.Count(x => x.RequestMessage.Path == path);
    }

    public async Task ResetStateAsync()
    {
        _wireMockServer.ResetMappings();
        _wireMockServer.ResetLogEntries();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [Orders]");
    }

    private string BuildConnectionString()
    {
        return $"{_sqlContainer.GetConnectionString().Replace("Database=master", $"Database={_databaseName}")};TrustServerCertificate=True";
    }
}
