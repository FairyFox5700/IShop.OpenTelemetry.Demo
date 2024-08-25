using OpenTelemetry.Trace;
using OpenTelemetryPricingSvc.Requests;
using OpenTelemetryProductSvc.Models;
using OpenTelemetryProductSvc.Repositories;
using OpenTelemetryProductSvc.Responces;
using OpenTelemetryProductSvc.Services;
public interface IProductService
{
    Task<IEnumerable<Product>> GetAllProductsAsync();
    Task<ProductWithPriceResponse> GetProductByIdAsync(Guid id);
    Task AddProductAsync(Product product);
    Task UpdateProductAsync(Guid id, Product updatedProduct);
    Task DeleteProductAsync(Guid id);
}

public class ProductService : IProductService
{
    private readonly ProductServiceMetrics _metrics;
    private readonly IProductRepository _productRepository;
    private readonly IPricingServiceClient _pricingServiceClient;
    private readonly Tracer _tracer;

    public ProductService(IProductRepository productRepository,
        IPricingServiceClient pricingServiceClient,
        TracerProvider tracerProvider,
        ProductServiceSettings productServiceSettings,
        ProductServiceMetrics metrics)
    {
        _productRepository = productRepository;
        _pricingServiceClient = pricingServiceClient;
        _tracer = tracerProvider.GetTracer(productServiceSettings.ServiceName);
        _metrics = metrics;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await _productRepository.GetAllAsync();
    }

    public async Task<ProductWithPriceResponse> GetProductByIdAsync(Guid id)
    {
        using var getProductSpan = _tracer?.StartActiveSpan("GetProductWithPrice", SpanKind.Server);

        getProductSpan?.SetAttribute("product.id", id.ToString());

        var product = await _productRepository.GetByIdAsync(id);

        if (product == null)
        {
            getProductSpan.SetStatus(Status.Error.WithDescription("Product not found"));
            throw new Exception("Product not found.");
        }

        var priceResponse = await _pricingServiceClient.GetPriceAsync(id);

        if (priceResponse == null)
        {
            getProductSpan?.SetStatus(Status.Error.WithDescription("Price information not found"));
            throw new Exception("Price information not found.");
        }

        var productWithPrice = new ProductWithPriceResponse
        {
            ProductId = product.Id,
            Name = product.Name,
            Price = priceResponse.CurrentPrice,
            DiscountedPrice = priceResponse.DiscountedPrice
        };

        getProductSpan?.SetAttribute("response.status", "success");
        getProductSpan?.SetAttribute("response.price", productWithPrice.Price.ToString());

        return productWithPrice;
    }

    public async Task AddProductAsync(Product product)
    {
        await _productRepository.AddAsync(product);
        _metrics.AddProduct();
        _metrics.IncreaseTotalProducts();
    }

    public async Task UpdateProductAsync(Guid id, Product updatedProduct)
    {
        if (id != updatedProduct.Id)
        {
            throw new ArgumentException("Product ID mismatch.");
        }

        var existingProduct = await _productRepository.GetByIdAsync(id);
        if (existingProduct == null)
        {
            throw new KeyNotFoundException("Product not found.");
        }

        await _productRepository.UpdateAsync(updatedProduct);

        if (updatedProduct.Price != existingProduct.Price)
        {
            var discountRequest = new DiscountApplyRequest
            {
                ProductId = updatedProduct.Id,
                DiscountType = "percentage", // Example: Adjust based on your discount logic
                DiscountValue = 10.0m, // Example: Adjust based on your business rules
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1) // Example: Set discount to end in one month
            };

            await _pricingServiceClient.ApplyDiscountAsync(discountRequest);
        }

        _metrics.UpdateProduct();
        _metrics.RecordProductPrice(updatedProduct.Price);
    }

    public async Task DeleteProductAsync(Guid id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        if (product == null)
        {
            throw new KeyNotFoundException("Product not found.");
        }
        await _productRepository.DeleteAsync(id);
        _metrics.DeleteProduct();
        _metrics.DecreaseTotalProducts();
    }
}
