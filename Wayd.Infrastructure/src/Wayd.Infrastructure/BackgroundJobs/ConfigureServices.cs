using Hangfire;
using Hangfire.Console;
using Hangfire.Console.Extensions;
using Hangfire.Dashboard.BasicAuthorization;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Wayd.Infrastructure.BackgroundJobs;

internal static class ConfigureServices
{
    private static readonly ILogger _logger = Log.ForContext(typeof(ConfigureServices));

    internal static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration config)
    {
        services.AddHangfireServer(options => config.GetSection("HangfireSettings:Server").Bind(options));

        services.AddHangfireConsoleExtensions();

        var storageSettings = config.GetSection("HangfireSettings:Storage").Get<HangfireStorageSettings>()!;

        if (string.IsNullOrEmpty(storageSettings.StorageProvider)) throw new Exception("Hangfire Storage Provider is not configured.");
        if (string.IsNullOrEmpty(storageSettings.ConnectionString)) throw new Exception("Hangfire Storage Provider ConnectionString is not configured.");
        _logger.Information("Hangfire: Current Storage Provider : {StorageProvider}", storageSettings.StorageProvider);

        services.AddSingleton<JobActivator, WaydJobActivator>();

        services.AddHangfire((provider, hangfireConfig) => hangfireConfig
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseActivator(provider.GetRequiredService<JobActivator>())
            .UseDatabase(storageSettings.StorageProvider, storageSettings.ConnectionString, config)
            .UseFilter(new WaydJobFilter(provider))
            .UseFilter(new LogJobFilter())
            .UseConsole());

        return services;
    }

    private static IGlobalConfiguration UseDatabase(this IGlobalConfiguration hangfireConfig, string dbProvider, string connectionString, IConfiguration config) =>
        dbProvider.ToLowerInvariant() switch
        {
            DbProviderKeys.SqlServer =>
                hangfireConfig.UseSqlServerStorage(connectionString, config.GetSection("HangfireSettings:Storage:Options").Get<SqlServerStorageOptions>()),
            //DbProviderKeys.Npgsql =>
            //    hangfireConfig.UsePostgreSqlStorage(connectionString, config.GetSection("HangfireSettings:Storage:Options").Get<PostgreSqlStorageOptions>()),
            _ => throw new Exception($"Hangfire Storage Provider {dbProvider} is not supported.")
        };

    internal static IApplicationBuilder UseHangfireDashboard(this IApplicationBuilder app, IConfiguration config)
    {
        var dashboardOptions = config.GetSection("HangfireSettings:Dashboard").Get<DashboardOptions>()!;
        var appPath = config["HangfireSettings:Dashboard:AppPath"];
        dashboardOptions.AppPath = string.IsNullOrWhiteSpace(appPath) ? null : appPath;

        dashboardOptions.Authorization = new[]
        {
            new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
            {
                SslRedirect = false,
                RequireSsl = false,
                LoginCaseSensitive = true,
                Users = new[]
                {
                    new BasicAuthAuthorizationUser
                    {
                        Login = config.GetSection("HangfireSettings:Credentials:User").Value,
                        PasswordClear = config.GetSection("HangfireSettings:Credentials:Password").Value
                    }
                }
            })
        };

        return app.UseHangfireDashboard(config["HangfireSettings:Route"], dashboardOptions);
    }
}