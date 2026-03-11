using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SampleApp.Data;
using Testcontainers.MsSql;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SampleApp.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory that points AppDbContext at a SQL Server
/// testcontainer using ConfigureTestServices.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private WireMockServer _wireMockServer = null!;
    private string _connectionString = null!;

    private string BuildConnectionString()
    {
        var baseConnectionString = _sqlContainer.GetConnectionString();
        var databaseName = $"SampleAppFactoryTests_{Guid.NewGuid():N}";
        return $"{baseConnectionString.Replace("Database=master", $"Database={databaseName}")};TrustServerCertificate=True";
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        _wireMockServer = WireMockServer.Start();
        _connectionString = BuildConnectionString();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _wireMockServer.Stop();
        _wireMockServer.Dispose();
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Catalog:BaseUrl", _wireMockServer.Url!);

        builder.ConfigureTestServices(services =>
        {
            // Replace the AppDbContext registration to use the SQL Server testcontainer.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(_connectionString);
            });
        });
    }

    public async Task CreateDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
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
}

