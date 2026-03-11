using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly string _databaseName = $"SampleAppTests_{Guid.NewGuid():N}";
    private WireMockServer _wireMockServer = null!;
    private WebApplication _app = null!;

    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _app.Services;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        _wireMockServer = WireMockServer.Start();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.WebHost.UseTestServer();

        var configurationOverrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] = BuildConnectionString(),
            ["Catalog:BaseUrl"] = _wireMockServer.Url!
        };
        builder.Configuration.AddInMemoryCollection(configurationOverrides);

        AppComposition.ConfigureServices(builder.Services, builder.Configuration);

        _app = builder.Build();
        AppComposition.ConfigureEndpoints(_app);

        using (var scope = _app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        await _app.StartAsync();
        Client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
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

        using var scope = _app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [Orders]");
    }

    private string BuildConnectionString()
    {
        return $"{_sqlContainer.GetConnectionString().Replace("Database=master", $"Database={_databaseName}")};TrustServerCertificate=True";
    }
}
