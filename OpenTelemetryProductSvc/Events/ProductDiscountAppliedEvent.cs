namespace OpenTelemetryProductSvc.Events
{

    // Event to indicate that a discount has been applied to a product
    public class ProductDiscountAppliedEvent
    {
        public Guid ProductId { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public DateTime AppliedAt { get; set; }
    }

}
