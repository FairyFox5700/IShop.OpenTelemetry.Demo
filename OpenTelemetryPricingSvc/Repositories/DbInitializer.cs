using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenTelemetryPricingSvc.Models;

namespace OpenTelemetryPricingSvc.Repositories
{
    public class DbInitializer
    {
        public static async Task InitializeAsync(PricingDbContext context)
        {
            // Apply any pending migrations
            await context.Database.MigrateAsync();

            // Seed Product Prices
            if (!context.ProductPrices.Any())
            {
                var productPrices = LoadSeedData<ProductPrice>("Data/SeedData/productPrices.json");
                foreach (var productPrice in productPrices)
                {
                    context.ProductPrices.Add(productPrice);
                }
                await context.SaveChangesAsync();
            }

            // Seed Discounts
            if (!context.Discounts.Any())
            {
                var discounts = LoadSeedData<Discount>("Data/SeedData/discounts.json");
                foreach (var discount in discounts)
                {
                    context.Discounts.Add(discount);
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
