namespace OpenTelemetryProductSvc.Models
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string UserId { get; set; }
    }
}
