namespace OpenTelemetryPricingSvc.Models
{
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
