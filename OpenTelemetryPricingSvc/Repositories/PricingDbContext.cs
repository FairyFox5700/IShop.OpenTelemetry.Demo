using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetryPricingSvc.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace OpenTelemetryPricingSvc.Repositories
{
    public class PricingDbContext : DbContext
    {
        public PricingDbContext(DbContextOptions<PricingDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProductPrice> ProductPrices { get; set; }
        public DbSet<Discount> Discounts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure the ProductPrice entity
            modelBuilder.Entity<ProductPrice>()
                .HasKey(pp => pp.ProductId);

            // Configure the Discount entity
            modelBuilder.Entity<Discount>()
                .HasKey(d => d.DiscountId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
