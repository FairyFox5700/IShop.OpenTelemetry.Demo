namespace OpenTelemetryPricingSvc.Requests
{
    public class PriceUpdateRequest
    {
        public Guid ProductId { get; set; } // Unique identifier for the product
        public decimal NewPrice { get; set; } // New price to be set
    }


}
