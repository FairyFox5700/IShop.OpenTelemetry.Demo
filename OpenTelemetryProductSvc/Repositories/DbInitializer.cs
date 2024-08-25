using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenTelemetryProductSvc.Models;

namespace OpenTelemetryProductSvc.Repositories
{
    public class DbInitializer
    {
        public static async Task InitializeAsync(ProductDbContext context)
        {
            context.Database.Migrate();

            // Seed Products
            if (!context.Products.Any())
            {
                var products = LoadSeedData<Product>("Data/SeedData/products.json");
                foreach (var product in products)
                {
                    product.Id = product.Id;
                    product.UserId = product.UserId;
                    context.Products.Add(product);
                }
                await context.SaveChangesAsync();
            }
        }

        private static List<T> LoadSeedData<T>(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<T>>(json);
        }
    }
}
