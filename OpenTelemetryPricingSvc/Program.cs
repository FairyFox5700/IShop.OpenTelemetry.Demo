using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryPricingSvc.Consumers;
using OpenTelemetryPricingSvc.Repositories;
using OpenTelemetryPricingSvc.Services;

var builder = WebApplication.CreateBuilder(args);
var pricingSettings = builder.Configuration.GetSection("PricingServiceSettings").Get<PricingServiceSettings>();
builder.Services.AddSingleton<PricingServiceSettings>(pricingSettings);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PricingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Scoped);

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

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<PricingDbContext>();

builder.Services.AddSingleton<PricingServiceMetrics>();
builder.Services.AddTransient<IProductPriceRepository, ProductPriceRepository>();
builder.Services.AddTransient<IPricingService, PricingService>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder => resourceBuilder
    .AddService(pricingSettings.ServiceName, serviceVersion: pricingSettings.ServiceVersion)
    .AddAttributes(new List<KeyValuePair<string, object>>
    {
       new KeyValuePair<string, object>("environment", builder.Environment.EnvironmentName)
    }))
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    })
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<PricingDbContext>();
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
app.MapHealthChecks("/healthz");
app.Run();
