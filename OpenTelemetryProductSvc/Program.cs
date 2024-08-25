using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryProductSvc.Consumers;
using OpenTelemetryProductSvc.Repositories;
using OpenTelemetryProductSvc.Services;
using OpenTelemetryShop.Repositories;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configuration Bindings
var productServiceSettings = builder.Configuration.GetSection("ProductServiceSettings").Get<ProductServiceSettings>();
builder.Services.AddSingleton<ProductServiceSettings>(productServiceSettings);


var otelMetricCollectorUrl = builder.Configuration["OtelMetricCollector:Host"];
var otelTraceCollectorUrl = builder.Configuration["OtelTraceCollector:Host"];

// Service Setup
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// The following line enables Application Insights telemetry collection.
builder.Services.AddApplicationInsightsTelemetry();
// Configure Database Context
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddTransient<IProductRepository, ProductRepository>();
builder.Services.AddTransient<IProductService, ProductService>();
// Configure Identity
builder.Services.AddScoped<ProductPriceUpdatedConsumer>();
// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductPriceUpdatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {

        cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
    {
        h.Username(builder.Configuration["RabbitMQ:Username"]);
        h.Password(builder.Configuration["RabbitMQ:Password"]);
    });

        // Add consumers if needed
        cfg.ReceiveEndpoint("product-updated-queue", e =>
    {
        e.ConfigureConsumer<ProductPriceUpdatedConsumer>(context);
    });
    });
});

builder.Services.Configure<PricingApiSettings>(builder.Configuration.GetSection("PricingApi"));
// Configure HTTP Client for Pricing Service
builder.Services.AddHttpClient<IPricingServiceClient, PricingServiceClient>(client =>
{
    var pricingApiSettings = builder.Configuration.GetSection("PricingApi").Get<PricingApiSettings>();
    client.BaseAddress = new Uri(pricingApiSettings.BaseUrl);
});


// Register ProductServiceMetrics
builder.Services.AddSingleton<ProductServiceMetrics>();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder => resourceBuilder
        .AddService(productServiceSettings.ServiceName, serviceVersion: productServiceSettings.ServiceVersion)
        .AddAttributes(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("environment", builder.Environment.EnvironmentName)
        }))
      .WithMetrics(metrics => metrics
        // Custom metrics provider
        .AddMeter(productServiceSettings.MeterName)
        // Metrics provides by ASP.NET Core in .NET 8
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otelMetricCollectorUrl);
            o.ExportProcessorType = ExportProcessorType.Batch;
            o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        })) // default port 4317
            //.AddConsoleExporter())
.WithTracing(t =>
{
    t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(productServiceSettings.ServiceName))
    .AddSource(productServiceSettings.ServiceName)
   .AddSource(nameof(ProductPriceUpdatedConsumer))
   .AddSource(nameof(ProductService))
   .AddSource(nameof(ProductRepository))
   .AddSource(nameof(ProductsController))
   .SetErrorStatusOnException()
   .SetSampler(new AlwaysOnSampler())
   .AddHttpClientInstrumentation()
   .AddAspNetCoreInstrumentation(opt => opt.RecordException = true)
   .AddEntityFrameworkCoreInstrumentation()
   .AddSqlClientInstrumentation()
   .AddOtlpExporter(o =>
   {
       o.ExportProcessorType = ExportProcessorType.Batch;
       o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
       o.Endpoint = new Uri(otelTraceCollectorUrl);
   })
  .AddConsoleExporter();
});


// Build the Application
var app = builder.Build();

app.MapDefaultEndpoints();


// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ProductDbContext>();
    await DbInitializer.InitializeAsync(context);
}


// Middleware Setup
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.MapPrometheusScrapingEndpoint();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public class PricingApiSettings
{
    public string BaseUrl { get; set; }
}