using Events;
using MassTransit;
using OpenTelemetryProductSvc.Events;
using OpenTelemetryProductSvc.Repositories;

namespace OpenTelemetryProductSvc.Consumers
{
    public class ProductPriceUpdatedConsumer : IConsumer<ProductPriceUpdatedEvent>
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<IProductRepository> _logger;

        public ProductPriceUpdatedConsumer(IProductRepository productRepository,
            ILogger<IProductRepository> logger)
        {
            _productRepository = productRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ProductPriceUpdatedEvent> context)
        {
            var message = context.Message;

            // Fetch the existing product record
            var product = await _productRepository.GetByIdAsync(message.ProductId);
            if (product != null)
            {
                // Update the product's price in the repository
                product.Price = message.NewPrice;

                await _productRepository.UpdateAsync(product);

                _logger.LogInformation($"Product price updated in product-svc for Product ID: {message.ProductId}, New Price: {message.NewPrice}");

            }

            await Task.CompletedTask;
        }
    }

}
