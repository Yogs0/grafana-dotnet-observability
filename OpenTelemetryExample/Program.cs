using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryExample.Migration;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Define service resource attributes
var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "OpenTelemetryExample";
var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName
    });

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(resourceBuilder)
            .AddSource("OpenTelemetryExample.WeatherForecast") // Custom application traces
            .AddSource("OpenTelemetryExample.LogMigration")    // Log migration traces
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("OpenTelemetryExample.WeatherForecast") // Custom application metrics
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
    });

// Configure Serilog with OpenTelemetry sink
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("service.name", serviceName)
    .Enrich.WithProperty("service.version", serviceVersion)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otlpEndpoint;
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = serviceName,
            ["service.version"] = serviceVersion,
            ["deployment.environment"] = builder.Environment.EnvironmentName
        };
    })
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register LogsDbContext for migration
var migrationConnectionString = builder.Configuration.GetConnectionString("LogsDatabase");
if (!string.IsNullOrEmpty(migrationConnectionString))
{
    builder.Services.AddDbContext<LogsDbContext>(options =>
        options.UseSqlServer(migrationConnectionString));
    builder.Services.AddHostedService<LogMigrationService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting OpenTelemetryExample application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
