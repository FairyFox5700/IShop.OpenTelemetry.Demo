using Microsoft.EntityFrameworkCore;
using OpenTelemetryPricingSvc.Models;

namespace OpenTelemetryPricingSvc.Repositories
{
    public interface IProductPriceRepository
    {
        Task<ProductPrice> GetProductPriceAsync(Guid productId);
        Task UpdateProductPriceAsync(ProductPrice productPrice);
        Task<IList<Discount>> GetActiveDiscountsAsync(Guid productId);
        Task ApplyDiscountAsync(Discount discount);
        Task AddProductPriceAsync(ProductPrice productPrice); // New method for adding product prices
    }

    public class ProductPriceRepository : IProductPriceRepository
    {
        private readonly PricingDbContext _context; // Replace with your actual DbContext

        public ProductPriceRepository(PricingDbContext context)
        {
            _context = context;
        }

        public async Task<ProductPrice> GetProductPriceAsync(Guid productId)
        {
            return await _context.ProductPrices
                .FirstOrDefaultAsync(p => p.ProductId == productId);
        }

        public async Task UpdateProductPriceAsync(ProductPrice productPrice)
        {
            var existingProductPrice = await _context.ProductPrices
                .FirstOrDefaultAsync(p => p.ProductId == productPrice.ProductId);

            if (existingProductPrice != null)
            {
                existingProductPrice.Price = productPrice.Price;
                existingProductPrice.LastUpdated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<Discount>> GetActiveDiscountsAsync(Guid productId)
        {
            return await _context.Discounts
                .Where(d => d.ProductId == productId && d.IsActive
                && d.StartDate <= DateTime.UtcNow && d.EndDate >= DateTime.UtcNow)
                .ToListAsync();
        }

        public async Task ApplyDiscountAsync(Discount discount)
        {
            await _context.Discounts.AddAsync(discount);
            await _context.SaveChangesAsync();
        }

        public async Task AddProductPriceAsync(ProductPrice productPrice) // Implementation of the new method
        {
            await _context.ProductPrices.AddAsync(productPrice);
            await _context.SaveChangesAsync();
        }
    }
}
