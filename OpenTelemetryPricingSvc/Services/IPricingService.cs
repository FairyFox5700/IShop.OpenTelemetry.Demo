using Events;
using OpenTelemetryPricingSvc.Requests;
using OpenTelemetryPricingSvc.Responces;

namespace OpenTelemetryPricingSvc.Services
{
    public interface IPricingService
    {
        Task AddPriceAsync(ProductAddedEvent request);
        Task ApplyDiscount(DiscountApplyRequest request);
        Task ApplyDiscountAsync(DiscountApplyRequest request);
        Task<PriceResponse> GetPrice(Guid productId);
        Task UpdatePriceAsync(PriceUpdateRequest request);
    }
}