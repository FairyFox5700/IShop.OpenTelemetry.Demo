namespace OpenTelemetryPricingSvc.Models
{
    public class Promotion
    {
        public Guid PromotionId { get; set; } // Unique identifier for the promotion
        public string Name { get; set; } // Name of the promotion
        public string Description { get; set; } // Description of the promotion
        public List<Discount> Discounts { get; set; } // List of discounts included in the promotion
        public DateTime StartDate { get; set; } // Start date of the promotion
        public DateTime EndDate { get; set; } // End date of the promotion
        public bool IsActive { get; set; } // Whether the promotion is currently active
    }
}
