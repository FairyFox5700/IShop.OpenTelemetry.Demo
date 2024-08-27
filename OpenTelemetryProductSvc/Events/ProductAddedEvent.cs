namespace Events
{
    public class ProductAddedEvent
    {
        public Guid Id { get; set; }
        public decimal Price { get; set; }
    }
}
