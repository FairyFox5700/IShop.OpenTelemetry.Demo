var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.OpenTelemetryPricingSvc>("opentelemetrypricingsvc");


builder.AddProject<Projects.OpenTelemetryProductSvc>("opentelemetryproductsvc");


builder.AddProject<Projects.UserManagementService>("usermanagementservice");


builder.Build().Run();
