namespace OpenTelemetryPricingSvc.Requests
{
    public class DiscountApplyRequest
    {
        public Guid ProductId { get; set; } // The unique identifier for the product
        public string DiscountType { get; set; } // The type of discount (e.g., "percentage", "fixed")
        public decimal DiscountValue { get; set; } // The value of the discount (e.g., 10 for 10%, or a fixed amount)
        public DateTime StartDate { get; set; } // The start date of the discount
        public DateTime EndDate { get; set; } // The end date of the discount
    }

}
