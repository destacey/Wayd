using System.Reflection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Wayd.Infrastructure.Logging;

internal static class ConfigureServices
{
    public static TBuilder AddLoggingDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog((serviceProvider, config) =>
        {
            config.ReadFrom.Configuration(builder.Configuration);

            // Add version enricher
            var version = Environment.GetEnvironmentVariable("version")
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "none-supplied";
            config.Enrich.WithProperty("version", version);

            // Add OpenTelemetry sink for Aspire dashboard when OTLP endpoint is configured
            var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                config.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = builder.Configuration["OTEL_SERVICE_NAME"] ?? "wayd-api"
                    };
                });
            }
        });

        if (builder.Environment.IsDevelopment())
        {
            Serilog.Debugging.SelfLog.Enable(Console.Error);
        }

        return builder;
    }
}
