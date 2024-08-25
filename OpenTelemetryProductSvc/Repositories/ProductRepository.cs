using Microsoft.EntityFrameworkCore;
using OpenTelemetryProductSvc.Models;
using OpenTelemetryProductSvc.Repositories;

namespace OpenTelemetryShop.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<ProductRepository> _logger;

        public ProductRepository(ProductDbContext context, ILogger<ProductRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            _logger.LogInformation("Fetching all products");
            var products = await _context.Products.ToListAsync();
            _logger.LogInformation("Fetched {Count} products", products.Count);
            return products;
        }

        public async Task<Product> GetByIdAsync(Guid id)
        {
            _logger.LogInformation("Fetching product with ID {ProductId}", id);
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found", id);
            }
            else
            {
                _logger.LogInformation("Fetched product with ID {ProductId}", id);
            }
            return product;
        }

        public async Task AddAsync(Product product)
        {
            _logger.LogInformation("Adding a new product with ID {ProductId}", product.Id);
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added product with ID {ProductId}", product.Id);
        }

        public async Task UpdateAsync(Product product)
        {
            _logger.LogInformation("Updating product with ID {ProductId}", product.Id);
            _context.Entry(product).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated product with ID {ProductId}", product.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            _logger.LogInformation("Deleting product with ID {ProductId}", id);
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted product with ID {ProductId}", id);
            }
            else
            {
                _logger.LogWarning("Product with ID {ProductId} not found for deletion", id);
            }
        }
    }

    /*
    static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Product {product} added")]
        public static partial void ProductAdded(this ILogger logger, [LogProperties]Product product);
    }
    */
}
