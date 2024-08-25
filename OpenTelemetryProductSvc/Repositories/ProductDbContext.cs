using Microsoft.EntityFrameworkCore;
using OpenTelemetryProductSvc.Models;

namespace OpenTelemetryProductSvc.Repositories
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Product>()
                .Property(p => p.Id)
                .HasDefaultValueSql("NEWID()");
        }
    }
}
