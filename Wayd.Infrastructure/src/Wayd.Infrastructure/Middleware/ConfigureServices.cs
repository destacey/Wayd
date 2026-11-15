using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Middleware;

internal static class ConfigureServices
{
    internal static IServiceCollection AddExceptionMiddleware(this IServiceCollection services) =>
        services.AddScoped<ExceptionMiddleware>();

    internal static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionMiddleware>();

    internal static IServiceCollection AddUserActivityTracking(this IServiceCollection services)
    {
        services.AddSingleton<LastSeenWriter>();
        services.AddHostedService(sp => sp.GetRequiredService<LastSeenWriter>());
        services.AddScoped<UserActivityTrackingMiddleware>();
        return services;
    }

    internal static IApplicationBuilder UseUserActivityTracking(this IApplicationBuilder app) =>
        app.UseMiddleware<UserActivityTrackingMiddleware>();

    internal static IServiceCollection AddRequestLogging(this IServiceCollection services, IConfiguration config)
    {
        if (GetMiddlewareSettings(config).EnableHttpsLogging)
            services.AddScoped<RequestLoggingMiddleware>();

        return services;
    }

    internal static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app, IConfiguration config)
    {
        if (GetMiddlewareSettings(config).EnableHttpsLogging)
        {
            app.UseMiddleware<RequestLoggingMiddleware>();
            ConfigureSerilogRequestLogging(app);
            // Turns an exception into its response inside the request log, as well as outside it in
            // UseInfrastructure: Serilog records any exception that reaches it as a 500 at Error, whatever
            // the response, so a validation failure (422) would log as a server fault.
            app.UseExceptionMiddleware();
        }

        return app;
    }

    private static void ConfigureSerilogRequestLogging(IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = EnrichDiagnosticContext;
            options.GetLevel = GetLogLevel;
        });
    }

    private static void EnrichDiagnosticContext(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path);
    }

    private static LogEventLevel GetLogLevel(HttpContext httpContext, double elapsed, Exception? ex)
    {
        return httpContext.Response.StatusCode switch
        {
            401 => LogEventLevel.Warning,
            403 => LogEventLevel.Warning,
            404 => LogEventLevel.Information,
            422 => LogEventLevel.Information,
            _ => ex != null || httpContext.Response.StatusCode > 400 ? LogEventLevel.Error : LogEventLevel.Information,
        };
    }

    private static MiddlewareSettings GetMiddlewareSettings(IConfiguration config) =>
        config.GetSection(nameof(MiddlewareSettings)).Get<MiddlewareSettings>()!;
}