using OpenTelemetryPricingSvc.Models;

namespace OpenTelemetryPricingSvc.Repositories
{
    public interface IProductPriceRepository
    {
        ProductPrice GetProductPrice(Guid productId);
        void UpdateProductPrice(ProductPrice productPrice);
        IEnumerable<Discount> GetActiveDiscounts(Guid productId);
        void ApplyDiscount(Discount discount);
    }

    public class ProductPriceRepository : IProductPriceRepository
    {
        private readonly PricingDbContext _context; // Replace with your actual DbContext

        public ProductPriceRepository(PricingDbContext context)
        {
            _context = context;
        }

        public ProductPrice GetProductPrice(Guid productId)
        {
            return _context.ProductPrices.SingleOrDefault(p => p.ProductId == productId);
        }

        public void UpdateProductPrice(ProductPrice productPrice)
        {
            var existingProductPrice = _context.ProductPrices.SingleOrDefault(p => p.ProductId == productPrice.ProductId);
            if (existingProductPrice != null)
            {
                existingProductPrice.Price = productPrice.Price;
                existingProductPrice.LastUpdated = DateTime.UtcNow;
                _context.SaveChanges();
            }
        }

        public IEnumerable<Discount> GetActiveDiscounts(Guid productId)
        {
            return _context.Discounts.Where(d => d.ProductId == productId && d.IsActive && d.StartDate <= DateTime.UtcNow && d.EndDate >= DateTime.UtcNow).ToList();
        }

        public void ApplyDiscount(Discount discount)
        {
            _context.Discounts.Add(discount);
            _context.SaveChanges();
        }
    }

}
