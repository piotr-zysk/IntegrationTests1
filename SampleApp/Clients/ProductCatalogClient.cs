using System.Net;
using System.Net.Http.Json;

namespace SampleApp.Clients;

public sealed class ProductCatalogClient(HttpClient httpClient) : IProductCatalogClient
{
    public async Task<CatalogProduct?> GetProductAsync(string productCode, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/products/{productCode}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogProduct>(cancellationToken);
    }
}
