using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using OpenTelemetryPricingSvc.Requests;

namespace OpenTelemetryProductSvc.Services
{
    public interface IPricingServiceClient
    {
        Task<PriceResponse> GetPriceAsync(Guid productId);
        Task UpdatePriceAsync(PriceUpdateRequest request);
        Task ApplyDiscountAsync(DiscountApplyRequest request);
    }

    public class PricingServiceClient : IPricingServiceClient
    {
        private readonly Tracer _tracer;
        private readonly HttpClient _httpClient;
        private readonly ProductServiceSettings _productServiceSettings;
        private readonly PricingApiSettings _pricingApiSettings;

        public PricingServiceClient(HttpClient httpClient,
            IOptions<PricingApiSettings> pricingApiSettings,
            ProductServiceSettings productServiceSettings,
            TracerProvider tracerProvider)
        {
            _httpClient = httpClient;
            _productServiceSettings = productServiceSettings;
            _pricingApiSettings = pricingApiSettings.Value;
            _tracer = tracerProvider.GetTracer(_productServiceSettings.ServiceName);
        }

        public async Task<PriceResponse> GetPriceAsync(Guid productId)
        {
            var requestUri = $"{_pricingApiSettings.BaseUrl}/Pricing/{productId}";
            var response = await _httpClient.GetAsync(requestUri);

            if (response.IsSuccessStatusCode)
            {
                // Deserialize response content to PriceResponse
                return await response.Content.ReadFromJsonAsync<PriceResponse>();
            }

            // Handle errors as needed
            // You might want to throw an exception or return a default/fallback value
            // For this example, returning null
            return null;
        }

        public async Task UpdatePriceAsync(PriceUpdateRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync("update", request);
            response.EnsureSuccessStatusCode();
        }

        // Method to apply discount with tracing
        public async Task ApplyDiscountAsync(DiscountApplyRequest request)
        {
            // Start a new span for the discount application operation
            using var applyDiscountSpan = _tracer.StartActiveSpan("ApplyDiscountAsync", SpanKind.Client);

            applyDiscountSpan.SetAttribute("comms", "api");
            applyDiscountSpan.SetAttribute("protocol", "http");

            try
            {
                var response = await _httpClient.PostAsJsonAsync("apply-discount", request);
                response.EnsureSuccessStatusCode();

                // Set span status to Ok if successful
                applyDiscountSpan.SetStatus(Status.Ok);
            }
            catch (Exception ex)
            {
                // Set span status to Error if an exception occurs
                applyDiscountSpan.SetStatus(Status.Error.WithDescription(ex.Message));
                throw; // Re-throw exception to ensure the caller is aware of the failure
            }
        }
    }

}
