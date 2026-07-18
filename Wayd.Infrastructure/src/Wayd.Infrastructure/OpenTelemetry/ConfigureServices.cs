using System.Data.Common;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Wayd.Infrastructure.OpenTelemetry;

internal static class ConfigureServices
{
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("Microsoft.FeatureManagement")
                    // Wolverine emits its own OTel activities for message send/handle under this source.
                    .AddSource("Wolverine")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Exclude health check requests from tracing
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(ServiceEndpoints.HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(ServiceEndpoints.AlivenessEndpointPath);
                        // Enable recording exception details
                        options.RecordException = true;
                    })
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.EnrichWithSqlCommand = (activity, commandObj) =>
                        {
                            if (commandObj is DbCommand command)
                            {
                                // Capture command type and SQL / stored procedure
                                activity.SetTag("db.statement", command.CommandText);
                                activity.SetTag("db.command_type", command.CommandType.ToString());

                                // Capture database name safely
                                var dbName = command.Connection?.Database;
                                if (!string.IsNullOrEmpty(dbName))
                                {
                                    activity.SetTag("db.name", dbName);
                                }

                                // Capture parameters (be careful with sensitive info)
                                if (command.Parameters != null)
                                {
                                    foreach (DbParameter param in command.Parameters)
                                    {
                                        if (param?.ParameterName != null)
                                        {
                                            activity.SetTag($"db.param.{param.ParameterName}", param.Value?.ToString());
                                        }
                                    }
                                }
                            }
                        };

                        // Capture exception events
                        options.RecordException = true;
                    });
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Enable Azure Monitor exporter for Application Insights when connection string is provided
        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry()
               .UseAzureMonitor();
        }

        return builder;
    }
}
