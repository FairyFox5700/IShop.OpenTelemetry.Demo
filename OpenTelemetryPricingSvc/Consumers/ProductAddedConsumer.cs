using Events;
using MassTransit;
using OpenTelemetryPricingSvc.Services;

namespace OpenTelemetryPricingSvc.Consumers
{
    public class ProductAddedConsumer : IConsumer<ProductAddedEvent>
    {
        private readonly IPricingService _pricingService;
        private readonly ILogger<ProductAddedConsumer> _logger;

        public ProductAddedConsumer(IPricingService pricingService,
            ILogger<ProductAddedConsumer> logger)
        {
            _pricingService = pricingService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ProductAddedEvent> context)
        {
            var message = context.Message;

            await _pricingService.AddPriceAsync(message);
            _logger.LogInformation($"Product price added to pricing with Product ID: {message.Id},  Price: {message.Price}");
            await Task.CompletedTask;
        }
    }
}
