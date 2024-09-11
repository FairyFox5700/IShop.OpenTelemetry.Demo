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
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

var otelMetricCollectorUrl = builder.Configuration["OtelMetricCollector:Host"];
var otelTraceCollectorUrl = builder.Configuration["OtelTraceCollector:Host"];

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
         .AddConsoleExporter()
         .SetSampler(new AlwaysOnSampler())
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
            .AddMeter(productServiceSettings.MeterName)
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
