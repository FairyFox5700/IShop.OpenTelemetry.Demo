using Microsoft.AspNetCore.Mvc;
using OpenTelemetryPricingSvc.Requests;
using OpenTelemetryPricingSvc.Responces;
using OpenTelemetryPricingSvc.Services;
using System.Diagnostics.Metrics;

namespace OpenTelemetryPricingSvc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PricingController : ControllerBase
    {
        private readonly IPricingService _pricingService;
        private readonly Counter<int> _priceUpdateCounter;
        private readonly Counter<int> _discountApplyCounter;
        public PricingController(IPricingService pricingService)
        {
            _pricingService = pricingService;
        }

        // Endpoint to get the current price of a product
        [HttpGet("{productId}")]
        public async Task<ActionResult<PriceResponse>> GetPrice(Guid productId)
        {
            var priceResponse = await _pricingService.GetPrice(productId);
            if (priceResponse == null)
            {
                return NotFound();
            }

            return Ok(priceResponse);
        }

        // Endpoint to update the price of a product
        [HttpPut("update")]
        public async Task<IActionResult> UpdatePrice([FromBody] PriceUpdateRequest request)
        {
            await _pricingService.UpdatePriceAsync(request);

            return NoContent();
        }

        // Endpoint to apply a discount to a product
        [HttpPost("apply-discount")]
        public async Task<IActionResult> ApplyDiscount([FromBody] DiscountApplyRequest request)
        {
            await _pricingService.ApplyDiscountAsync(request);

            return NoContent();
        }
    }

}
