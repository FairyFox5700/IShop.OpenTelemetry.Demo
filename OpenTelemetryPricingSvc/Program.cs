using Azure.Monitor.OpenTelemetry.AspNetCore;
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

// configure healt checks
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<PricingDbContext>();


builder.Services.AddSingleton<PricingServiceMetrics>();
// Configure Repositories and Services
builder.Services.AddTransient<IProductPriceRepository, ProductPriceRepository>();
builder.Services.AddTransient<IPricingService, PricingService>();

builder.Services.AddApplicationInsightsTelemetry();
// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
     .UseAzureMonitor()
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
        //TODO uncomment case u need to use prometheus exporter
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
   .AddHttpClientInstrumentation(options =>
   {
       options.EnrichWithHttpRequestMessage = (activity, request) =>
       {
           activity.SetTag("http.method", request.Method.Method); // Capture the HTTP method (GET, POST, etc.)
           activity.SetTag("http.url", request.RequestUri.ToString()); // Capture the full URL
           activity.SetTag("http.host", request.RequestUri.Host); // Capture the host part of the URL
           activity.SetTag("http.path", request.RequestUri.AbsolutePath); // Capture the path of the URL
           activity.SetTag("http.query", request.RequestUri.Query); // Capture the query string

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
           activity.SetTag("http.status_code", (int)response.StatusCode); // Capture the HTTP status code (e.g., 200, 404)
           activity.SetTag("http.status_text", response.ReasonPhrase); // Capture the reason phrase associated with the status code
           activity.SetTag("http.response_length", response.Content.Headers.ContentLength); // Capture the length of the response content
           activity.SetTag("http.response_content_type", response.Content.Headers.ContentType?.ToString()); // Capture the content type of the response

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
    .AddEntityFrameworkCoreInstrumentation(options =>
    {
        options.EnrichWithIDbCommand = (activity, command) =>
        {
            // Set a more descriptive display name for the activity
            var stateDisplayName = $"{command.CommandType}";
            activity.DisplayName = stateDisplayName;

            // Set common tags for better traceability and monitoring
            activity.SetTag("db.name", command.Connection.Database); // Set the database name
            activity.SetTag("db.statement", command.CommandText); // Set the SQL command
            activity.SetTag("db.command_type", command.CommandType.ToString()); // Set the command type (Text, StoredProcedure, etc.)
            activity.SetTag("db.connection_string", command.Connection.ConnectionString); // Optionally set the connection string (consider security implications)
            activity.SetTag("db.execution_time", DateTime.UtcNow); // Capture the timestamp of execution
            activity.SetTag("db.parameters", string.Join(", ", command.Parameters.Cast<DbParameter>().Select(p => $"{p.ParameterName}: {p.Value}"))); // Capture parameters
        };
    })
    .AddAspNetCoreInstrumentation(opt => opt.RecordException = true)
    .SetErrorStatusOnException()
    .AddOtlpExporter(o =>
    {
        o.ExportProcessorType = ExportProcessorType.Batch;
        o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        o.Endpoint = new Uri(otelTraceCollectorUrl);
    })
   .AddConsoleExporter();
});

var app = builder.Build();

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
app.MapHealthChecks("/healthz");
app.Run();
