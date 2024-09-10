using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryProductSvc.Consumers;
using OpenTelemetryProductSvc.Repositories;
using OpenTelemetryProductSvc.Services;
using OpenTelemetryShop.Repositories;

var builder = WebApplication.CreateBuilder(args);

var productServiceSettings = builder.Configuration.GetSection("ProductServiceSettings").Get<ProductServiceSettings>();
builder.Services.AddSingleton<ProductServiceSettings>(productServiceSettings);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddTransient<IProductRepository, ProductRepository>();
builder.Services.AddTransient<IProductService, ProductService>();
builder.Services.AddScoped<ProductPriceUpdatedConsumer>();
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
        cfg.ReceiveEndpoint("product-updated-queue", e =>
    {
        e.ConfigureConsumer<ProductPriceUpdatedConsumer>(context);
    });
    });
});

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<PricingApiSettings>(builder.Configuration.GetSection("PricingApi"));
builder.Services.AddHttpClient<IPricingServiceClient, PricingServiceClient>(client =>
{
    var pricingApiSettings = builder.Configuration.GetSection("PricingApi").Get<PricingApiSettings>();
    client.BaseAddress = new Uri(pricingApiSettings.BaseUrl);
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ProductDbContext>();

builder.Services.AddSingleton<ProductServiceMetrics>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder => resourceBuilder
    .AddService(productServiceSettings.ServiceName, serviceVersion: productServiceSettings.ServiceVersion)
    .AddAttributes(new List<KeyValuePair<string, object>>
    {
       new KeyValuePair<string, object>("environment", builder.Environment.EnvironmentName)
    }))

    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
         .AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .SetSampler(new AlwaysOnSampler()) // 5% sampling rate
         .AddConsoleExporter();
    })
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .AddMeter(productServiceSettings.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter()
            .AddConsoleExporter();
    });

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ProductDbContext>();
    await DbInitializer.InitializeAsync(context);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
