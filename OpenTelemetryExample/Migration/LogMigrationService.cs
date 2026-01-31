using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenTelemetryExample.Migration.Entities;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace OpenTelemetryExample.Migration;

public class LogMigrationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogMigrationService> _logger;

    // ActivitySource for tracing migration operations
    private static readonly ActivitySource ActivitySource = new("OpenTelemetryExample.LogMigration");

    public LogMigrationService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<LogMigrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var migrationConfig = _configuration.GetSection("LogMigration");

        if (!migrationConfig.GetValue<bool>("Enabled"))
        {
            _logger.LogInformation("Log migration is disabled");
            return;
        }

        using var activity = ActivitySource.StartActivity("LogMigration");

        _logger.LogInformation("Starting log migration from SQL Server to Loki via OpenTelemetry...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await MigrateLogsAsync(cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Log migration completed successfully in {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);

            activity?.SetTag("migration.status", "success");
            activity?.SetTag("migration.duration_ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Log migration failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            activity?.SetTag("migration.status", "failed");
            activity?.SetTag("migration.error", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateLogsAsync(CancellationToken cancellationToken)
    {
        var otlpEndpoint = _configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var batchSize = _configuration.GetValue<int>("LogMigration:BatchSize", 1000);

        // Create a dedicated Serilog logger for migration using OpenTelemetry sink
        // This sends logs through OTEL Collector → Loki (same pipeline as app logs)
        using var migrationLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("job", "sql-migration")
            .WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
                options.Protocol = OtlpProtocol.Grpc;
                options.BatchingOptions.BatchSizeLimit = batchSize;
                options.BatchingOptions.BufferingTimeLimit = TimeSpan.FromMilliseconds(500);
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = "LogMigration",
                    ["service.version"] = "1.0.0"
                };
            })
            .CreateLogger();

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LogsDbContext>();

        var migrateAuditTrail = _configuration.GetValue<bool>("LogMigration:MigrateAuditTrail", true);
        var migrateApiLogs = _configuration.GetValue<bool>("LogMigration:MigrateApiLogs", true);

        if (migrateAuditTrail)
        {
            await MigrateAuditTrailLogsAsync(dbContext, migrationLogger, batchSize, cancellationToken);
        }

        if (migrateApiLogs)
        {
            await MigrateApiLogsAsync(dbContext, migrationLogger, batchSize, cancellationToken);
        }

        // Ensure all batched logs are flushed
        await migrationLogger.DisposeAsync();
        await Task.Delay(1000, cancellationToken);
    }

    private async Task MigrateAuditTrailLogsAsync(
        LogsDbContext dbContext,
        Serilog.ILogger migrationLogger,
        int batchSize,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("MigrateAuditTrailLogs");

        var totalCount = await dbContext.AuditTrailLogs.CountAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} audit trail logs...", totalCount);

        activity?.SetTag("migration.table", "Log_AuditTrail_T");
        activity?.SetTag("migration.total_count", totalCount);

        var processed = 0;
        var lastId = 0;

        while (processed < totalCount)
        {
            var batch = await dbContext.AuditTrailLogs
                .AsNoTracking()
                .Where(l => l.Id > lastId)
                .OrderBy(l => l.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0) break;

            foreach (var log in batch)
            {
                var level = ParseLogLevel(log.Level);

                // Write migrated log with all original properties preserved
                migrationLogger
                    .ForContext("source", "Log_AuditTrail_T")
                    .ForContext("service_name", "AuditTrail")
                    .ForContext("original_timestamp", log.TimeStamp)
                    .ForContext("userId", log.UserId)
                    .ForContext("userIp", log.UserIp)
                    .ForContext("device", log.Device)
                    .ForContext("browser", log.Browser)
                    .ForContext("platform", log.Platform)
                    .ForContext("crawler", log.Crawler)
                    .ForContext("application", log.Application)
                    .ForContext("applicationVersion", log.ApplicationVersion)
                    .ForContext("userAgent", log.UserAgent)
                    .Write(level, log.Exception != null ? new Exception(log.Exception) : null,
                        "{Message}", log.Message ?? "");

                lastId = log.Id;
            }

            processed += batch.Count;
            _logger.LogInformation("Migrated {Processed}/{Total} audit trail logs", processed, totalCount);
        }

        activity?.SetTag("migration.processed_count", processed);
    }

    private async Task MigrateApiLogsAsync(
        LogsDbContext dbContext,
        Serilog.ILogger migrationLogger,
        int batchSize,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("MigrateApiLogs");

        var totalCount = await dbContext.ApiLogs.CountAsync(cancellationToken);
        _logger.LogInformation("Migrating {Count} API logs...", totalCount);

        activity?.SetTag("migration.table", "LogsApi");
        activity?.SetTag("migration.total_count", totalCount);

        var processed = 0;
        var lastId = 0;

        while (processed < totalCount)
        {
            var batch = await dbContext.ApiLogs
                .AsNoTracking()
                .Where(l => l.Id > lastId)
                .OrderBy(l => l.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0) break;

            foreach (var log in batch)
            {
                var level = ParseLogLevel(log.Level);

                var logContext = migrationLogger
                    .ForContext("source", "LogsApi")
                    .ForContext("service_name", "ApiLogs")
                    .ForContext("original_timestamp", log.TimeStamp);

                // Parse and add properties if present
                if (!string.IsNullOrEmpty(log.Properties))
                {
                    try
                    {
                        var props = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.Properties);
                        if (props != null)
                        {
                            foreach (var prop in props)
                            {
                                logContext = logContext.ForContext(prop.Key, prop.Value.ToString());
                            }
                        }
                    }
                    catch
                    {
                        logContext = logContext.ForContext("properties", log.Properties);
                    }
                }

                logContext.Write(level, log.Exception != null ? new Exception(log.Exception) : null,
                    "{Message}", log.Message ?? "");

                lastId = log.Id;
            }

            processed += batch.Count;
            _logger.LogInformation("Migrated {Processed}/{Total} API logs", processed, totalCount);
        }

        activity?.SetTag("migration.processed_count", processed);
    }

    private static LogEventLevel ParseLogLevel(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" or "critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
