using System.Diagnostics.Metrics;

namespace OpenTelemetryProductSvc.Services
{
    public class ProductServiceMetrics
    {
        // Product meters
        private Counter<int> ProductsAddedCounter { get; }
        private Counter<int> ProductsDeletedCounter { get; }
        private Counter<int> ProductsUpdatedCounter { get; }
        private UpDownCounter<int> TotalProductsUpDownCounter { get; }

        // Product distribution meters
        private Histogram<decimal> ProductPriceHistogram { get; }

        public ProductServiceMetrics(IMeterFactory meterFactory, ProductServiceSettings productServiceSettings)
        {
            var meter = meterFactory.Create(productServiceSettings.MeterName ??
                                            throw new NullReferenceException("ProductService meter missing a name"));

            ProductsAddedCounter = meter.CreateCounter<int>("products-added", "Product");
            ProductsDeletedCounter = meter.CreateCounter<int>("products-deleted", "Product");
            ProductsUpdatedCounter = meter.CreateCounter<int>("products-updated", "Product");
            TotalProductsUpDownCounter = meter.CreateUpDownCounter<int>("total-products", "Product");

            ProductPriceHistogram = meter.CreateHistogram<decimal>("product-price", "Dollars", "Price distribution of products");
        }

        // Product meters
        public void AddProduct() => ProductsAddedCounter.Add(1);
        public void DeleteProduct() => ProductsDeletedCounter.Add(1);
        public void UpdateProduct() => ProductsUpdatedCounter.Add(1);
        public void IncreaseTotalProducts() => TotalProductsUpDownCounter.Add(1);
        public void DecreaseTotalProducts() => TotalProductsUpDownCounter.Add(-1);


        // Product distribution meters
        public void RecordProductPrice(decimal price) => ProductPriceHistogram.Record(price);
    }
}
