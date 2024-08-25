namespace OpenTelemetryProductSvc.Services
{
    public class PriceResponse
    {
        public Guid ProductId { get; set; } // Unique identifier for the product
        public decimal CurrentPrice { get; set; } // Current price of the product
        public decimal? DiscountedPrice { get; set; } // Discounted price, if applicable
        public List<Discount> ActiveDiscounts { get; set; } // List of active discounts on the product
    }

    public class Discount
    {
        public Guid DiscountId { get; set; }
        public Guid ProductId { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }
}