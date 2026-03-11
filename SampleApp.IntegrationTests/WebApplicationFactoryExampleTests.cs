using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SampleApp.IntegrationTests;

/// <summary>
/// Example-only tests showing how to use WebApplicationFactory&lt;Program&gt;.
/// These are skipped by default so they don't require any specific infrastructure.
/// </summary>
public sealed class WebApplicationFactoryExampleTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(Skip = "Example of WebApplicationFactory usage for learning; enable when you want to run it.")]
    public async Task GetRoot_WithRealHost_ShouldReturnAStatusCode()
    {
        var response = await _client.GetAsync("/");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound);
    }
}

