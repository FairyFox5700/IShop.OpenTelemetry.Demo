using System.Diagnostics.Metrics;

namespace OpenTelemetryPricingSvc.Services
{
    public class PricingServiceMetrics
    {
        // Pricing meters
        private Counter<int> PricesAddedCounter { get; }
        private Counter<int> PricesDeletedCounter { get; }
        private Counter<int> PricesUpdatedCounter { get; }
        private UpDownCounter<int> TotalPricesUpDownCounter { get; }

        // Discount meters
        private Counter<int> DiscountsAppliedCounter { get; }
        private ObservableGauge<int> ActiveDiscountsGauge { get; }
        private int _activeDiscounts = 0;

        // Price distribution meters
        private Histogram<decimal> PriceAmountHistogram { get; }
        private Histogram<int> PriceChangeFrequencyHistogram { get; }

        // Initialization
        public PricingServiceMetrics(IMeterFactory meterFactory, PricingServiceSettings pricingServiceSettings)
        {
            var meter = meterFactory.Create(pricingServiceSettings.MeterName ??
                                            throw new NullReferenceException("PricingService meter missing a name"));

            PricesAddedCounter = meter.CreateCounter<int>("prices-added", "Price");
            PricesDeletedCounter = meter.CreateCounter<int>("prices-deleted", "Price");
            PricesUpdatedCounter = meter.CreateCounter<int>("prices-updated", "Price");
            TotalPricesUpDownCounter = meter.CreateUpDownCounter<int>("total-prices", "Price");

            DiscountsAppliedCounter = meter.CreateCounter<int>("discounts-applied", "Discount");
            ActiveDiscountsGauge = meter.CreateObservableGauge<int>("active-discounts", () => _activeDiscounts);

            PriceAmountHistogram = meter.CreateHistogram<decimal>("price-amount", "Dollars", "Price distribution of products");
            PriceChangeFrequencyHistogram = meter.CreateHistogram<int>("price-change-frequency", "Changes", "Frequency of price changes");
        }

        // Pricing meters
        public void AddPrice() => PricesAddedCounter.Add(1);
        public void DeletePrice() => PricesDeletedCounter.Add(1);
        public void UpdatePrice() => PricesUpdatedCounter.Add(1);
        public void IncreaseTotalPrices() => TotalPricesUpDownCounter.Add(1);
        public void DecreaseTotalPrices() => TotalPricesUpDownCounter.Add(-1);

        // Discount meters
        public void ApplyDiscount() => DiscountsAppliedCounter.Add(1);
        public void IncreaseActiveDiscounts() => _activeDiscounts++;
        public void DecreaseActiveDiscounts() => _activeDiscounts--;

        // Price distribution meters
        public void RecordPriceAmount(decimal amount) => PriceAmountHistogram.Record(amount);
        public void RecordPriceChangeFrequency(int frequency) => PriceChangeFrequencyHistogram.Record(frequency);
    }
}
