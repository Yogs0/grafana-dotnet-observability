using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var sqlConnectionString = config["SqlServer:ConnectionString"]!;
var lokiEndpoint = config["Loki:Endpoint"] ?? "http://localhost:3100";
var batchSize = int.Parse(config["Migration:BatchSize"] ?? "1000");

AnsiConsole.Write(new FigletText("Log Migration").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[bold]SQL Server to Loki Migration Tool[/]");
AnsiConsole.WriteLine();

using var httpClient = new HttpClient { BaseAddress = new Uri(lokiEndpoint) };

// Menu selection
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Which table do you want to migrate?")
        .AddChoices("Log_AuditTrail_T", "LogsApi", "Both tables", "Exit"));

if (choice == "Exit") return;

if (choice is "Log_AuditTrail_T" or "Both tables")
{
    await MigrateAuditTrailLogs();
}

if (choice is "LogsApi" or "Both tables")
{
    await MigrateApiLogs();
}

AnsiConsole.MarkupLine("[green]Migration completed![/]");

async Task MigrateAuditTrailLogs()
{
    AnsiConsole.MarkupLine("\n[bold cyan]Migrating Log_AuditTrail_T...[/]");

    await using var connection = new SqlConnection(sqlConnectionString);
    await connection.OpenAsync();

    // Get total count
    await using var countCmd = new SqlCommand("SELECT COUNT(*) FROM Log_AuditTrail_T", connection);
    var totalCount = (int)await countCmd.ExecuteScalarAsync()!;

    if (totalCount == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No records found in Log_AuditTrail_T[/]");
        return;
    }

    AnsiConsole.MarkupLine($"[dim]Found {totalCount} records[/]");

    var offset = 0;
    var migrated = 0;

    await AnsiConsole.Progress()
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Migrating audit logs[/]", maxValue: totalCount);

            while (offset < totalCount)
            {
                var query = $@"
                    SELECT Id, Message, Level, TimeStamp, Exception, UserId, UserIp,
                           Device, Browser, Platform, Crawler, Application, ApplicationVersion, UserAgent
                    FROM Log_AuditTrail_T
                    ORDER BY Id
                    OFFSET {offset} ROWS FETCH NEXT {batchSize} ROWS ONLY";

                await using var cmd = new SqlCommand(query, connection);
                await using var reader = await cmd.ExecuteReaderAsync();

                var streams = new List<LokiStream>();
                var currentStream = new LokiStream
                {
                    Stream = new Dictionary<string, string>
                    {
                        ["job"] = "sql-migration",
                        ["source"] = "Log_AuditTrail_T",
                        ["service_name"] = "AuditTrail"
                    },
                    Values = []
                };

                while (await reader.ReadAsync())
                {
                    var timestamp = reader.GetDateTime(reader.GetOrdinal("TimeStamp"));
                    var level = reader.IsDBNull(reader.GetOrdinal("Level")) ? "Information" : reader.GetString(reader.GetOrdinal("Level"));
                    var message = reader.IsDBNull(reader.GetOrdinal("Message")) ? "" : reader.GetString(reader.GetOrdinal("Message"));
                    var exception = reader.IsDBNull(reader.GetOrdinal("Exception")) ? null : reader.GetString(reader.GetOrdinal("Exception"));

                    var logEntry = new Dictionary<string, object?>
                    {
                        ["level"] = level,
                        ["message"] = message,
                        ["exception"] = exception,
                        ["userId"] = reader.IsDBNull(reader.GetOrdinal("UserId")) ? null : reader.GetValue(reader.GetOrdinal("UserId")),
                        ["userIp"] = reader.IsDBNull(reader.GetOrdinal("UserIp")) ? null : reader.GetString(reader.GetOrdinal("UserIp")),
                        ["device"] = reader.IsDBNull(reader.GetOrdinal("Device")) ? null : reader.GetString(reader.GetOrdinal("Device")),
                        ["browser"] = reader.IsDBNull(reader.GetOrdinal("Browser")) ? null : reader.GetString(reader.GetOrdinal("Browser")),
                        ["platform"] = reader.IsDBNull(reader.GetOrdinal("Platform")) ? null : reader.GetString(reader.GetOrdinal("Platform")),
                        ["crawler"] = reader.IsDBNull(reader.GetOrdinal("Crawler")) ? null : reader.GetValue(reader.GetOrdinal("Crawler")),
                        ["application"] = reader.IsDBNull(reader.GetOrdinal("Application")) ? null : reader.GetString(reader.GetOrdinal("Application")),
                        ["applicationVersion"] = reader.IsDBNull(reader.GetOrdinal("ApplicationVersion")) ? null : reader.GetString(reader.GetOrdinal("ApplicationVersion")),
                        ["userAgent"] = reader.IsDBNull(reader.GetOrdinal("UserAgent")) ? null : reader.GetString(reader.GetOrdinal("UserAgent"))
                    };

                    // Remove null values
                    var cleanedEntry = logEntry.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value);

                    var nanoseconds = new DateTimeOffset(timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds() * 1_000_000;
                    currentStream.Values.Add([nanoseconds.ToString(), JsonSerializer.Serialize(cleanedEntry)]);

                    migrated++;
                }

                if (currentStream.Values.Count > 0)
                {
                    streams.Add(currentStream);
                    await PushToLoki(streams);
                }

                task.Value = migrated;
                offset += batchSize;
            }
        });

    AnsiConsole.MarkupLine($"[green]Migrated {migrated} audit trail logs[/]");
}

async Task MigrateApiLogs()
{
    AnsiConsole.MarkupLine("\n[bold cyan]Migrating LogsApi...[/]");

    await using var connection = new SqlConnection(sqlConnectionString);
    await connection.OpenAsync();

    // Get total count
    await using var countCmd = new SqlCommand("SELECT COUNT(*) FROM LogsApi", connection);
    var totalCount = (int)await countCmd.ExecuteScalarAsync()!;

    if (totalCount == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No records found in LogsApi[/]");
        return;
    }

    AnsiConsole.MarkupLine($"[dim]Found {totalCount} records[/]");

    var offset = 0;
    var migrated = 0;

    await AnsiConsole.Progress()
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Migrating API logs[/]", maxValue: totalCount);

            while (offset < totalCount)
            {
                var query = $@"
                    SELECT Id, Message, Level, TimeStamp, Exception, Properties
                    FROM LogsApi
                    ORDER BY Id
                    OFFSET {offset} ROWS FETCH NEXT {batchSize} ROWS ONLY";

                await using var cmd = new SqlCommand(query, connection);
                await using var reader = await cmd.ExecuteReaderAsync();

                var streams = new List<LokiStream>();
                var currentStream = new LokiStream
                {
                    Stream = new Dictionary<string, string>
                    {
                        ["job"] = "sql-migration",
                        ["source"] = "LogsApi",
                        ["service_name"] = "ApiLogs"
                    },
                    Values = []
                };

                while (await reader.ReadAsync())
                {
                    var timestamp = reader.GetDateTime(reader.GetOrdinal("TimeStamp"));
                    var level = reader.IsDBNull(reader.GetOrdinal("Level")) ? "Information" : reader.GetString(reader.GetOrdinal("Level"));
                    var message = reader.IsDBNull(reader.GetOrdinal("Message")) ? "" : reader.GetString(reader.GetOrdinal("Message"));
                    var exception = reader.IsDBNull(reader.GetOrdinal("Exception")) ? null : reader.GetString(reader.GetOrdinal("Exception"));
                    var properties = reader.IsDBNull(reader.GetOrdinal("Properties")) ? null : reader.GetString(reader.GetOrdinal("Properties"));

                    var logEntry = new Dictionary<string, object?>
                    {
                        ["level"] = level,
                        ["message"] = message,
                        ["exception"] = exception
                    };

                    // Parse and merge properties if it's JSON
                    if (!string.IsNullOrEmpty(properties))
                    {
                        try
                        {
                            var props = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(properties);
                            if (props != null)
                            {
                                foreach (var prop in props)
                                {
                                    logEntry[prop.Key] = prop.Value.ToString();
                                }
                            }
                        }
                        catch
                        {
                            logEntry["properties"] = properties;
                        }
                    }

                    // Remove null values
                    var cleanedEntry = logEntry.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value);

                    var nanoseconds = new DateTimeOffset(timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds() * 1_000_000;
                    currentStream.Values.Add([nanoseconds.ToString(), JsonSerializer.Serialize(cleanedEntry)]);

                    migrated++;
                }

                if (currentStream.Values.Count > 0)
                {
                    streams.Add(currentStream);
                    await PushToLoki(streams);
                }

                task.Value = migrated;
                offset += batchSize;
            }
        });

    AnsiConsole.MarkupLine($"[green]Migrated {migrated} API logs[/]");
}

async Task PushToLoki(List<LokiStream> streams)
{
    var payload = new { streams };
    var json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await httpClient.PostAsync("/loki/api/v1/push", content);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        AnsiConsole.MarkupLine($"[red]Loki push failed: {response.StatusCode} - {error}[/]");
    }
}

class LokiStream
{
    public Dictionary<string, string> Stream { get; set; } = new();
    public List<string[]> Values { get; set; } = [];
}
