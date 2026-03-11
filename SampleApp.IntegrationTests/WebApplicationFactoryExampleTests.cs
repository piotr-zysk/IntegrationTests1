using System.Net;

namespace SampleApp.IntegrationTests;

/// <summary>
/// Example tests showing how to use WebApplicationFactory&lt;Program&gt;
/// with a custom factory that swaps the real SQL Server instance for a
/// SQL Server testcontainer via ConfigureTestServices.
/// </summary>
public sealed class WebApplicationFactoryExampleTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetRoot_WithRealHost_ShouldReturnAStatusCode()
    {
        var response = await _client.GetAsync("/");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound);
    }
}

