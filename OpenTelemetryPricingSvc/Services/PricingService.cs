using Events;
using MassTransit;
using MassTransit.Transports;
using OpenTelemetryPricingSvc.Events;
using OpenTelemetryPricingSvc.Models;
using OpenTelemetryPricingSvc.Repositories;
using OpenTelemetryPricingSvc.Requests;
using OpenTelemetryPricingSvc.Responces;

namespace OpenTelemetryPricingSvc.Services
{
    public interface IPricingService
    {
        PriceResponse GetPrice(Guid productId);
        Task ApplyDiscountAsync(DiscountApplyRequest request);
        Task UpdatePriceAsync(PriceUpdateRequest request);
    }

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

        public async Task UpdatePriceAsync(PriceUpdateRequest request)
        {
            var productPrice = _productPriceRepository.GetProductPrice(request.ProductId);
            if (productPrice != null)
            {
                productPrice.Price = request.NewPrice;
                productPrice.LastUpdated = DateTime.UtcNow;
                _productPriceRepository.UpdateProductPrice(productPrice);

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

            _productPriceRepository.ApplyDiscount(discount);
        }

        public PriceResponse GetPrice(Guid productId)
        {
            var productPrice = _productPriceRepository.GetProductPrice(productId);
            var activeDiscounts = _productPriceRepository.GetActiveDiscounts(productId).ToList();

            decimal? discountedPrice = productPrice.Price;
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
                CurrentPrice = productPrice.Price,
                DiscountedPrice = discountedPrice,
                ActiveDiscounts = activeDiscounts
            };
        }

        public void ApplyDiscount(DiscountApplyRequest request)
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

            _productPriceRepository.ApplyDiscount(discount);
            _metrics.ApplyDiscount();
            _metrics.IncreaseActiveDiscounts();
        }
    }

}
