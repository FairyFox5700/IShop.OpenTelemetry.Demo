namespace OpenTelemetryPricingSvc.Models
{
    public class ProductPrice
    {
        public Guid ProductId { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public DateTime LastUpdated { get; set; }
    }

}
