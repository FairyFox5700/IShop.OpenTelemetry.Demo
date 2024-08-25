namespace OpenTelemetryProductSvc.Responces
{
    public class ProductWithPriceResponse
    {
        public Guid ProductId { get; set; } // The unique identifier for the product
        public string Name { get; set; } // The name of the product
        public string Description { get; set; } // The description of the product
        public decimal Price { get; set; } // The current price of the product
        public decimal? DiscountedPrice { get; set; } // The price after applying active discounts, if any
    }

}
