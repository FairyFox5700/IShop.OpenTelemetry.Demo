using Azure.Monitor.OpenTelemetry.AspNetCore;
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
builder.Services.AddApplicationInsightsTelemetry();
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

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<PricingApiSettings>(builder.Configuration.GetSection("PricingApi"));
// Configure HTTP Client for Pricing Service
builder.Services.AddHttpClient<IPricingServiceClient, PricingServiceClient>(client =>
{
    var pricingApiSettings = builder.Configuration.GetSection("PricingApi").Get<PricingApiSettings>();
    client.BaseAddress = new Uri(pricingApiSettings.BaseUrl);
});
// configure healt checks
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ProductDbContext>();

// Register ProductServiceMetrics
builder.Services.AddSingleton<ProductServiceMetrics>();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor()
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
   .SetErrorStatusOnException()
   .SetSampler(new AlwaysOnSampler())
      .AddHttpClientInstrumentation(options =>
      {
          options.EnrichWithHttpRequestMessage = (activity, request) =>
          {
              // Set common tags for better traceability and monitoring
              activity.SetTag("http.method", request.Method.Method); // Capture the HTTP method (GET, POST, etc.)
              activity.SetTag("http.url", request.RequestUri.ToString()); // Capture the full URL
              activity.SetTag("http.host", request.RequestUri.Host); // Capture the host part of the URL
              activity.SetTag("http.path", request.RequestUri.AbsolutePath); // Capture the path of the URL
              activity.SetTag("http.query", request.RequestUri.Query); // Capture the query string

              // Optional: Capture and log request headers
              foreach (var header in request.Headers)
              {
                  activity.SetTag($"http.request_header.{header.Key}", string.Join(", ", header.Value));
              }

              // Optional: Capture additional custom tags
              if (request.Content != null)
              {
                  activity.SetTag("http.request_content_length", request.Content.Headers.ContentLength?.ToString());
                  activity.SetTag("http.request_content_type", request.Content.Headers.ContentType?.ToString());
              }
          };

          options.EnrichWithHttpResponseMessage = (activity, response) =>
          {
              // Set common tags for better traceability and monitoring
              activity.SetTag("http.status_code", (int)response.StatusCode); // Capture the HTTP status code (e.g., 200, 404)
              activity.SetTag("http.status_text", response.ReasonPhrase); // Capture the reason phrase associated with the status code
              activity.SetTag("http.response_length", response.Content.Headers.ContentLength); // Capture the length of the response content
              activity.SetTag("http.response_content_type", response.Content.Headers.ContentType?.ToString()); // Capture the content type of the response

              // Optional: Capture and log response headers
              foreach (var header in response.Headers)
              {
                  activity.SetTag($"http.response_header.{header.Key}", string.Join(", ", header.Value));
              }

              // Optional: Capture additional custom tags
              if (response.Content != null)
              {
                  // Capture the first few bytes of the response content as a preview (useful for small responses)
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