using OpenTelemetryPricingSvc.Models;

namespace OpenTelemetryPricingSvc.Responces
{
    public class ProductWithPriceResponse
    {
        public Guid ProductId { get; set; } // The unique identifier for the product
        public string Name { get; set; } // The name of the product
        public string Description { get; set; } // The description of the product
        public decimal Price { get; set; } // The current price of the product
        public decimal? DiscountedPrice { get; set; } // The price after applying active discounts, if any
        public List<Discount> ActiveDiscounts { get; set; } // A list of active discounts applied to the product
    }

}
