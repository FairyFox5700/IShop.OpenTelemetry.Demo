var builder = DistributedApplication.CreateBuilder(args);


var db = builder.AddContainer("db", "mcr.microsoft.com/mssql/server:2019-latest")
    .WithEntrypoint("db")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("SA_PASSWORD", "YourStrong!Passw0rd")
    .WithEnvironment("MSSQL_PID", "Express")
    .WithEndpoint(1433, 1433)
    .WithVolume("dbdata", "/var/opt/mssql");


var graphana = builder.AddContainer("graphana", "grafana/grafana")
    .WithEntrypoint("graphana")
    .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
    .WithEnvironment("GF_FEATURE_TOGGLES_ENABLE", "traceqlEditor")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
    .WithVolume("./grafana-datasources.yaml", "/etc/grafana/provisioning/datasources/datasources.yaml")
    .WithEndpoint(3000, 3000);

var prometheus = builder.AddContainer("prometheus", "prom/prometheus")
    .WithEndpoint(9090, 9090)
    .WithEntrypoint("prometheus")
    .WithContainerRuntimeArgs("--config.file=/etc/prometheus/prometheus-config.yaml")
    .WithContainerRuntimeArgs("--web.enable-remote-write-receiver")
    .WithContainerRuntimeArgs("--enable-feature=exemplar-storage")
    .WithVolume("./prometheus-config.yaml", "/etc/prometheus/prometheus-config.yaml");


var loki = builder.AddContainer("loki", "grafana/loki:latest")
    .WithEntrypoint("loki")
    .WithContainerRuntimeArgs("-config.file=/etc/loki/local-config.yaml")
    .WithEndpoint(3100, 3100);

var jagger = builder.AddContainer("jagger", "jaegertracing/all-in-one:latest")
    .WithEndpoint(port: 5775, targetPort: 5775, scheme: "udp", name: "agent-zipkin-thrift")
    .WithEndpoint(port: 6831, targetPort: 6831, scheme: "udp", name: "agent-jaeger-compact")
    .WithEndpoint(port: 6832, targetPort: 6832, scheme: "udp", name: "agent-jaeger-binary")
    .WithEndpoint(port: 5778, targetPort: 5778, scheme: "http", name: "agent-configs")
    .WithEndpoint(port: 16686, targetPort: 16686, scheme: "http", name: "query-frontend")
    .WithEndpoint(port: 14268, targetPort: 14268, scheme: "http", name: "collector-jaeger")
    .WithEndpoint(port: 9411, targetPort: 9411, scheme: "http", name: "collector-zipkin");

var rabbitMq = builder.AddContainer("rabbitmq", "rabbitmq:3-management")
    .WithEntrypoint("rabbitmq")
    .WithEndpoint(port: 5672, targetPort: 5672, scheme: "tcp", name: "amqp")
    .WithEndpoint(port: 15672, targetPort: 15672, scheme: "http", name: "management-ui");

var pricing = builder.AddProject<Projects.OpenTelemetryPricingSvc>("opentelemetrypricingsvc");

builder.AddProject<Projects.OpenTelemetryProductSvc>("opentelemetryproductsvc")
    .WithReference(pricing)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://aspire-dashboard:18889");


/*
    environment:
      SA_PASSWORD: 
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"
    volumes:
      - dbdata:/var/opt/mssql

  open_telemetry_product_svc:
    image: open_telemetry_product_svc
    build:
      context: .
      dockerfile: OpenTelemetryProductSvc/Dockerfile
    environment:
     - ASPNETCORE_ENVIRONMENT=Development
     - ConnectionStrings__DefaultConnection=Server=db;Database=PricingDb;User=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;
     - OtelMetricCollector__Host=http://aspire-dashboard:18889
     - OtelTraceCollector__Host=http://aspire-dashboard:18889
     - APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=24548519-ec60-4cfb-b882-a22bebc0248d;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.com/;ApplicationId=df994b33-78a7-4720-8130-df0512d072bb
     - RabbitMQ__Host=rabiitmq_shop
     - PricingApi__BaseUrl=http://open_telemetry_pricing_svc/api
     - OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889
    ports:
      - "5000:80"
    volumes:
      - ${APPDATA}/Microsoft/UserSecrets:/root/.microsoft/usersecrets:ro
      - ${APPDATA}/ASP.NET/Https:/root/.aspnet/https:ro

 * */
builder.AddProject<Projects.UserManagementService>("usermanagementservice");
builder.Build().Run();
