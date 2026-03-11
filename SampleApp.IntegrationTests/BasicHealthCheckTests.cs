using System.Net;

namespace SampleApp.IntegrationTests;

public sealed class BasicHealthCheckTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task GetRoot_ShouldReturnNotFoundOrSuccess()
    {
        var client = fixture.Client;

        var response = await client.GetAsync("/");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound);
    }
}

