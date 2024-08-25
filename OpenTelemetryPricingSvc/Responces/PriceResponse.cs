using OpenTelemetryPricingSvc.Models;

namespace OpenTelemetryPricingSvc.Responces
{
    public class PriceResponse
    {
        public Guid ProductId { get; set; } // Unique identifier for the product
        public decimal CurrentPrice { get; set; } // Current price of the product
        public decimal? DiscountedPrice { get; set; } // Discounted price, if applicable
        public List<Discount> ActiveDiscounts { get; set; } // List of active discounts on the product
    }


}
