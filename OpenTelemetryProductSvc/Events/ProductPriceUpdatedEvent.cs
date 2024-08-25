namespace Events
{
    // Event to indicate that a product's price has been updated
    public class ProductPriceUpdatedEvent
    {
        public Guid ProductId { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

}
