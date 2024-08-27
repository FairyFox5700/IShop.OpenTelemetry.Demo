using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryPricingSvc.Consumers;
using OpenTelemetryPricingSvc.Repositories;
using OpenTelemetryPricingSvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configuration Bindings
var pricingSettings = builder.Configuration.GetSection("PricingServiceSettings").Get<PricingServiceSettings>();
builder.Services.AddSingleton<PricingServiceSettings>(pricingSettings);
var otelMetricCollectorUrl = builder.Configuration["OtelMetricCollector:Host"];
var otelTraceCollectorUrl = builder.Configuration["OtelTraceCollector:Host"];

// Service Setup
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Database Context
builder.Services.AddDbContext<PricingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Scoped);

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductAddedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]);
            h.Password(builder.Configuration["RabbitMQ:Password"]);
        });

        cfg.ReceiveEndpoint("product-added-queue", e =>
        {
            e.ConfigureConsumer<ProductAddedConsumer>(context);
        });
    });
});

builder.Services.AddSingleton<PricingServiceMetrics>();
// Configure Repositories and Services
builder.Services.AddTransient<IProductPriceRepository, ProductPriceRepository>();
builder.Services.AddTransient<IPricingService, PricingService>();

builder.Services.AddApplicationInsightsTelemetry();
// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder => resourceBuilder
        .AddService(pricingSettings.ServiceName, serviceVersion: pricingSettings.ServiceVersion)
        .AddAttributes(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("environment", builder.Environment.EnvironmentName)
        }))
        .WithMetrics(metrics => metrics
        // Custom metrics provider
        .AddMeter(pricingSettings.MeterName)
        // Metrics provides by ASP.NET Core in .NET 8
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        //.AddAzureMonitorMetricExporter()
        /*.AddPrometheusExporter(opt =>
        {
            // Disable the default metric name suffix for counters
            opt.DisableTotalNameSuffixForCounters = true;
        })*/
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otelMetricCollectorUrl);
            o.ExportProcessorType = ExportProcessorType.Batch;
            o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        })) // default port 4317
            //.AddConsoleExporter())
.WithTracing(t =>
{
    t.AddSource(pricingSettings.ServiceName)
    .SetErrorStatusOnException()
    .SetSampler(new AlwaysOnSampler())
    .AddHttpClientInstrumentation()
    .AddEntityFrameworkCoreInstrumentation()
    .AddSqlClientInstrumentation()
    .AddAspNetCoreInstrumentation(opt => opt.RecordException = true)
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
    var context = services.GetRequiredService<PricingDbContext>();
    await DbInitializer.InitializeAsync(context);
}

// Middleware Setup
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
