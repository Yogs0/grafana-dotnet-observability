using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetryExample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        // Custom ActivitySource for manual tracing
        private static readonly ActivitySource ActivitySource = new("OpenTelemetryExample.WeatherForecast");

        // Custom Meter for application-specific metrics
        private static readonly Meter Meter = new("OpenTelemetryExample.WeatherForecast");
        private static readonly Counter<long> ForecastRequestCounter = Meter.CreateCounter<long>("weather_forecast_requests", "requests", "Number of weather forecast requests");
        private static readonly Histogram<double> ForecastGenerationDuration = Meter.CreateHistogram<double>("weather_forecast_generation_duration", "ms", "Time to generate forecast");

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            // Increment custom counter
            ForecastRequestCounter.Add(1, new KeyValuePair<string, object?>("endpoint", "GetWeatherForecast"));

            _logger.LogInformation ("Weather forecast requested at {RequestTime}", DateTime.UtcNow);

            var stopwatch = Stopwatch.StartNew();

            // Create a custom span for the forecast generation
            using var activity = ActivitySource.StartActivity("GenerateWeatherForecast");
            activity?.SetTag("forecast.days", 5);

            var forecasts = Enumerable.Range(1, 5).Select(index =>
            {
                var temp = Random.Shared.Next(-20, 55);
                var summary = Summaries[Random.Shared.Next(Summaries.Length)];

                // Log each forecast generation with structured logging
                _logger.LogDebug("Generated forecast for day {Day}: {Temperature}C - {Summary}",
                    index, temp, summary);

                return new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = temp,
                    Summary = summary
                };
            }).ToArray();

            stopwatch.Stop();

            // Record custom histogram metric
            ForecastGenerationDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

            activity?.SetTag("forecast.count", forecasts.Length);
            activity?.SetTag("forecast.avg_temp", forecasts.Average(f => f.TemperatureC));

            _logger.LogInformation("Weather forecast generated successfully with {Count} items in {Duration}ms",
                forecasts.Length, stopwatch.Elapsed.TotalMilliseconds);

            return forecasts;
        }
    }
}
