using Hangfire;
using Hangfire.Console;
using Hangfire.Console.Extensions;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Wayd.Infrastructure.BackgroundJobs;

internal static class ConfigureServices
{
    private static readonly ILogger _logger = Log.ForContext(typeof(ConfigureServices));

    internal static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration config)
    {
        // A host built only to be introspected must not touch the database. The Hangfire *server* is a
        // hosted service that resolves JobStorage on start, and SqlServerStorage installs its schema in
        // its constructor - so against a missing or unmigrated database this turns a Debug *build*
        // (the build boots the real app to read the OpenAPI document) into a build failure. The client-side
        // registrations below stay: they resolve storage lazily, and nothing enqueues a job while the
        // OpenAPI document is being read.
        if (!HostIntrospection.SkipsDatabaseInitialization(config))
        {
            services.AddHangfireServer(options => config.GetSection("HangfireSettings:Server").Bind(options));
        }

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
}
