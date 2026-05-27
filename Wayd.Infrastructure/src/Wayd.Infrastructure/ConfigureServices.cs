using System.Diagnostics;
using System.Reflection;
using Asp.Versioning;
using Mapster;
using Mapster.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.FeatureManagement;
using Wayd.Infrastructure.Logging;
using Wayd.Infrastructure.OpenTelemetry;
using Wayd.Infrastructure.SignalR;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;
using Wayd.Integrations.AzureDevOps;
using Wayd.Integrations.MicrosoftGraph;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using NodaTime;

namespace Wayd.Infrastructure;

public static class ConfigureServices
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddLoggingDefaults();

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        var assembly = Assembly.GetExecutingAssembly();
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        services.AddSingleton<IClock>(SystemClock.Instance);

        services.AddMemoryCache();

        // Data protection (at-rest secret encryption) must initialize before
        // persistence so the EF model can pick up the protector via SecretProtectorAccessor.
        services.AddDataProtectionForSecrets(config);

        // INTEGRATIONS
        services.AddTransient<IAzureDevOpsService, AzureDevOpsService>();
        services.AddScoped<IEntraEmployeeSource, MicrosoftGraphService>();

        // Generic sync orchestration: one IWorkItemSource and one descriptor builder per connector.
        // (IWorkItemSourceFactory is auto-registered via the IScopedService marker scan.)
        services.AddKeyedTransient<IWorkItemSource, AzureDevOpsWorkItemSource>(Connector.AzureDevOps);
        services.AddScoped<ISyncableConnectionDescriptorBuilder, AzureDevOpsConnectionDescriptorBuilder>();

        // SIGNALR
        var signalRBuilder = services.AddSignalR();
        var signalRConnectionString = config.GetValue<string>("Azure:SignalR:ConnectionString");
        if (!string.IsNullOrWhiteSpace(signalRConnectionString))
        {
            signalRBuilder.AddAzureSignalR();
        }
        services.AddScoped<IPokerSessionNotifier, PlanningPokerNotifier>();

        return services
            .AddApiVersioning()
            .AddAuth(config, environment)
            .AddBackgroundJobs(config)
            .AddCorsPolicy(config)
            .AddUserActivityTracking()
            .AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = (context) =>
                {
                    context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
                    context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);

                    Activity? activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
                    context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);
                };
            })
            .AddExceptionMiddleware()
            .AddWaydFeatureManagement()
            .AddMediatR(options => options.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()))
            .AddOpenApiDocumentation(config)
            .AddPersistence(config)
            .AddRequestLogging(config)
            .AddRouting(options => options.LowercaseUrls = true)
            .AddServices();
    }

    private static IServiceCollection AddApiVersioning(this IServiceCollection services) =>
        services.AddApiVersioning(config =>
        {
            config.DefaultApiVersion = new ApiVersion(1, 0);
            config.AssumeDefaultVersionWhenUnspecified = true;
            config.ReportApiVersions = true;
        }).Services;

    private static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static async Task InitializeDatabases(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // Create a new scope to retrieve scoped services
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>()
            .InitializeDatabase(cancellationToken);
    }

    /// <summary>
    /// Must be called after <see cref="InitializeDatabases"/>.
    /// Generates and logs a one-time setup token when no users exist.
    /// </summary>
    public static async Task RunBootstrapCheck(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var tokenService = services.GetRequiredService<Wayd.Infrastructure.Auth.Bootstrap.BootstrapTokenService>();
        var logger = services.GetRequiredService<ILogger<Wayd.Infrastructure.Auth.Bootstrap.BootstrapTokenService>>();
        await Wayd.Infrastructure.Auth.Bootstrap.BootstrapService.RunAsync(services, tokenService, logger, cancellationToken);
    }

    public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder builder, IConfiguration config) =>
        builder
            .UseStaticFiles()
            .UseSecurityHeaders(config)
            //.UseStatusCodePages()
            .UseExceptionMiddleware()
            //.UseHttpsRedirection() // TODO: we don't currently need this because we are using docker.  Add a config setting to enable this when needed.
            .UseRouting()
            .UseCorsPolicy()
            .UseAuthentication()
            .UseAuthorization()
            .UseUserActivityTracking()
            .UseRequestLogging(config) // TODO: we currently don't log 403 logs because it is lower in the middleware pipeline. It should be above UseRouting, but then we don't get user information.
            .UseHangfireDashboard(config)
            .UseOpenApiDocumentation(config);



    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapControllers().RequireAuthorization();
        app.MapHub<PlanningPokerHub>("/hubs/planning-poker");

        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(ServiceEndpoints.HealthEndpointPath);
        }
        else
        {
            app.MapHealthChecks(ServiceEndpoints.HealthEndpointPath).RequireAuthorization();
        }

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks(ServiceEndpoints.AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        app.MapGet(ServiceEndpoints.StartupEndpointPath, () => Results.Ok());

        return app;
    }
}
