using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryPricingSvc.Consumers;
using OpenTelemetryPricingSvc.Repositories;
using OpenTelemetryPricingSvc.Services;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);
var pricingSettings = builder.Configuration.GetSection("PricingServiceSettings").Get<PricingServiceSettings>();
builder.Services.AddSingleton<PricingServiceSettings>(pricingSettings);

var otelMetricCollectorUrl = builder.Configuration["OtelMetricCollector:Host"];
var otelTraceCollectorUrl = builder.Configuration["OtelTraceCollector:Host"];

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
            .SetSampler(new AlwaysOnSampler())
            .AddConsoleExporter()
            .AddHttpClientInstrumentation(options =>
            {
                options.EnrichWithHttpRequestMessage = (activity, request) =>
                {
                    activity.SetTag("http.method", request.Method.Method);
                    activity.SetTag("http.url", request.RequestUri.ToString());
                    activity.SetTag("http.host", request.RequestUri.Host);
                    activity.SetTag("http.path", request.RequestUri.AbsolutePath);
                    activity.SetTag("http.query", request.RequestUri.Query);

                    foreach (var header in request.Headers)
                    {
                        activity.SetTag($"http.request_header.{header.Key}", string.Join(", ", header.Value));
                    }

                    if (request.Content != null)
                    {
                        activity.SetTag("http.request_content_length", request.Content.Headers.ContentLength?.ToString());
                        activity.SetTag("http.request_content_type", request.Content.Headers.ContentType?.ToString());
                    }
                };

                options.EnrichWithHttpResponseMessage = (activity, response) =>
                {

                    activity.SetTag("http.status_code", (int)response.StatusCode);
                    activity.SetTag("http.status_text", response.ReasonPhrase);
                    activity.SetTag("http.response_length", response.Content.Headers.ContentLength);
                    activity.SetTag("http.response_content_type", response.Content.Headers.ContentType?.ToString());

                    foreach (var header in response.Headers)
                    {
                        activity.SetTag($"http.response_header.{header.Key}", string.Join(", ", header.Value));
                    }


                    if (response.Content != null)
                    {
                        var previewLength = 100;
                        var responsePreview = response.Content.ReadAsStringAsync().Result.Substring(0, Math.Min(previewLength, (int)response.Content.Headers.ContentLength));
                        activity.SetTag("http.response_preview", responsePreview);
                    }
                };
            })
            .AddAspNetCoreInstrumentation(opt =>
            {
                opt.Filter = ctx =>
                {
                    return ctx.Request.Path.StartsWithSegments("/api");
                };
                opt.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.EnrichWithIDbCommand = (activity, command) =>
                {
                    var stateDisplayName = $"{command.CommandType}";
                    activity.DisplayName = stateDisplayName;

                    activity.SetTag("db.name", command.Connection.Database);
                    activity.SetTag("db.statement", command.CommandText);
                    activity.SetTag("db.command_type", command.CommandType.ToString());
                    activity.SetTag("db.connection_string", command.Connection.ConnectionString);
                    activity.SetTag("db.execution_time", DateTime.UtcNow);
                    activity.SetTag("db.parameters", string.Join(", ", command.Parameters.Cast<DbParameter>().Select(p => $"{p.ParameterName}: {p.Value}")));
                };
            })
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otelTraceCollectorUrl);
                o.ExportProcessorType = ExportProcessorType.Batch;
                o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
    })
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .AddMeter(pricingSettings.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otelMetricCollectorUrl);
                o.ExportProcessorType = ExportProcessorType.Batch;
                o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
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
