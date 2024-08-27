using Events;
using MassTransit;
using OpenTelemetryPricingSvc.Models;
using OpenTelemetryPricingSvc.Repositories;
using OpenTelemetryPricingSvc.Requests;
using OpenTelemetryPricingSvc.Responces;

namespace OpenTelemetryPricingSvc.Services
{
    public class PricingService : IPricingService
    {
        private readonly IProductPriceRepository _productPriceRepository;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly PricingServiceMetrics _metrics;

        public PricingService(IProductPriceRepository productPriceRepository,
            PricingServiceMetrics metrics,
            IPublishEndpoint publishEndpoint)
        {
            _productPriceRepository = productPriceRepository;
            _publishEndpoint = publishEndpoint;
            _metrics = metrics;
        }

        public async Task AddPriceAsync(ProductAddedEvent request)
        {
            _metrics.RecordPriceAmount(request.Price);
            _metrics.RecordPriceChangeFrequency(1);
            _metrics.UpdatePrice();
            await _productPriceRepository.AddProductPriceAsync(new ProductPrice
            {
                Price = request.Price,
                ProductId = request.Id,
                DiscountedPrice = request.Price,
                LastUpdated = DateTime.UtcNow
            });

        }

        public async Task UpdatePriceAsync(PriceUpdateRequest request)
        {
            var productPrice = await _productPriceRepository.GetProductPriceAsync(request.ProductId);
            if (productPrice != null)
            {
                productPrice.Price = request.NewPrice;
                productPrice.LastUpdated = DateTime.UtcNow;
                await _productPriceRepository.UpdateProductPriceAsync(productPrice);

                // Record the price change amount and frequency
                _metrics.RecordPriceAmount(request.NewPrice);
                _metrics.RecordPriceChangeFrequency(1);

                // Publish an event when the price is updated
                await _publishEndpoint.Publish(new ProductPriceUpdatedEvent
                {
                    ProductId = request.ProductId,
                    NewPrice = request.NewPrice,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            _metrics.UpdatePrice();
        }

        public async Task ApplyDiscountAsync(DiscountApplyRequest request)
        {
            var discount = new Discount
            {
                DiscountId = Guid.NewGuid(),
                ProductId = request.ProductId,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = true
            };

            await _productPriceRepository.ApplyDiscountAsync(discount);
        }

        public async Task<PriceResponse> GetPrice(Guid productId)
        {
            var productPrice = await _productPriceRepository.GetProductPriceAsync(productId);
            var activeDiscounts = (await _productPriceRepository.GetActiveDiscountsAsync(productId))?.ToList();

            decimal? discountedPrice = productPrice?.Price;
            foreach (var discount in activeDiscounts)
            {
                if (discount.DiscountType == "percentage")
                {
                    discountedPrice -= discountedPrice * (discount.DiscountValue / 100);
                }
                else if (discount.DiscountType == "fixed")
                {
                    discountedPrice -= discount.DiscountValue;
                }
            }

            return new PriceResponse
            {
                ProductId = productId,
                CurrentPrice = productPrice?.Price ?? 0,
                DiscountedPrice = discountedPrice,
                ActiveDiscounts = activeDiscounts
            };
        }

        public async Task ApplyDiscount(DiscountApplyRequest request)
        {
            var discount = new Discount
            {
                DiscountId = Guid.NewGuid(),
                ProductId = request.ProductId,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = true
            };

            await _productPriceRepository.ApplyDiscountAsync(discount);
            _metrics.ApplyDiscount();
            _metrics.IncreaseActiveDiscounts();
        }
    }

}
