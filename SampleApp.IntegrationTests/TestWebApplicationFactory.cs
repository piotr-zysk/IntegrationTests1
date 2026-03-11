using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleApp.Data;
using Testcontainers.MsSql;

namespace SampleApp.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory that points AppDbContext at a SQL Server
/// testcontainer using ConfigureTestServices.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private string BuildConnectionString()
    {
        var baseConnectionString = _sqlContainer.GetConnectionString();
        return $"{baseConnectionString.Replace("Database=master", "Database=SampleAppFactoryTests")};TrustServerCertificate=True";
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
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
                options.UseSqlServer(BuildConnectionString());
            });
        });
    }
}

