namespace SampleApp.Clients;

public interface IProductCatalogClient
{
    Task<CatalogProduct?> GetProductAsync(string productCode, CancellationToken cancellationToken);
}
