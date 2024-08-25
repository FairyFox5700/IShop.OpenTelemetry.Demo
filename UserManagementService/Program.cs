using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text;
using UserManagementService.GraphQl;
using UserManagementService.Models;
using UserManagementService.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
var userServiceSettings = builder.Configuration.GetSection("UserServiceSettings").Get<UserServiceSettings>();
builder.Services.AddSingleton<UserServiceSettings>(userServiceSettings);

var otelMetricCollectorUrl = builder.Configuration["OtelMetricCollector:Host"];
var otelTraceCollectorUrl = builder.Configuration["OtelTraceCollector:Host"];

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]);

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = false,
        ValidateLifetime = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };

    options.SaveToken = true;
});
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddGraphQLServer()
    .AddAuthorization()
    .AddInstrumentation()
    .AddQueryType<UserManagementService.GraphQl.Query>()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = true)
    .AddMutationType<Mutation>();

builder.Services.AddControllers();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder => resourceBuilder
        .AddService(userServiceSettings.ServiceName, serviceVersion: userServiceSettings.ServiceVersion)
        .AddAttributes(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("environment", builder.Environment.EnvironmentName)
        }))
      .WithMetrics(metrics => metrics
        // Custom metrics provider
        .AddMeter(userServiceSettings.MeterName)
        // Metrics provides by ASP.NET Core in .NET 8
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        /* .AddPrometheusExporter(opt =>
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
    t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(userServiceSettings.ServiceName))
    .AddSource(userServiceSettings.ServiceName)
   .SetErrorStatusOnException()
   .SetSampler(new AlwaysOnSampler())
   .AddHttpClientInstrumentation()
   .AddAspNetCoreInstrumentation(opt => opt.RecordException = true)
   .AddHotChocolateInstrumentation()
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

var app = builder.Build();

app.MapDefaultEndpoints();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    await DbInitializer.InitializeAsync(context, userManager);
}


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGraphQL();
    endpoints.MapControllers();
});

app.Run();

public class UserServiceSettings
{
    public string MeterName { get; set; }
    public string ServiceName { get; set; }
    public string ServiceVersion { get; set; }
}
