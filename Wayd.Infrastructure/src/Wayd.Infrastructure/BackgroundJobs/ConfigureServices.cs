using Hangfire;
using Hangfire.Common;
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

        // The host's own storage and filters, registered before AddHangfire, which adds its own only when none is.
        // Its defaults are the process-wide JobStorage.Current and global filter list, and a test process runs several
        // hosts side by side, each on a database of its own: these keep each host's jobs on its own database and
        // WaydJobFilter on its own services. The server, the job client and the recurring-job manager all take them
        // from the container; the callback below still sets the statics, which nothing here reads. SqlServerStorage
        // installs its schema in its constructor, so it is built only when first resolved.
        services.AddSingleton<JobStorage>(_ => CreateStorage(storageSettings.StorageProvider, storageSettings.ConnectionString, config));
        services.AddSingleton<IJobFilterProvider>(provider => new JobFilterProviderCollection(
            JobFilterProviders.Providers,
            new JobFilterCollection { new WaydJobFilter(provider), new LogJobFilter() }));

        services.AddHangfire((provider, hangfireConfig) => hangfireConfig
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseActivator(provider.GetRequiredService<JobActivator>())
            .UseStorage(provider.GetRequiredService<JobStorage>())
            .UseConsole());

        return services;
    }

    private static JobStorage CreateStorage(string dbProvider, string connectionString, IConfiguration config) =>
        dbProvider.ToLowerInvariant() switch
        {
            DbProviderKeys.SqlServer =>
                new SqlServerStorage(connectionString, config.GetSection("HangfireSettings:Storage:Options").Get<SqlServerStorageOptions>() ?? new SqlServerStorageOptions()),
            //DbProviderKeys.Npgsql =>
            //    new PostgreSqlStorage(connectionString, config.GetSection("HangfireSettings:Storage:Options").Get<PostgreSqlStorageOptions>()),
            _ => throw new Exception($"Hangfire Storage Provider {dbProvider} is not supported.")
        };
}
