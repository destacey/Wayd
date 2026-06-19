using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Goals.Application.Persistence;
using Wayd.Links;
using Wayd.Planning.Application.Persistence;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.StrategicManagement.Application;
using Wayd.Work.Application.Persistence;
using Serilog;

namespace Wayd.Infrastructure.Persistence;

internal static class ConfigureServices
{
    private static readonly ILogger _logger = Log.ForContext(typeof(ConfigureServices));

    internal static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        // TODO: there must be a cleaner way to do IOptions validation...
        var databaseSettings = config.GetSection(nameof(DatabaseSettings)).Get<DatabaseSettings>();
        if (databaseSettings is null)
            throw new InvalidOperationException("DatabaseSettings is not configured.");

        string? rootConnectionString = databaseSettings.ConnectionString;
        if (string.IsNullOrWhiteSpace(rootConnectionString))
            throw new InvalidOperationException("DB ConnectionString is not configured.");

        string? dbProvider = databaseSettings.DBProvider;
        if (string.IsNullOrWhiteSpace(dbProvider))
            throw new InvalidOperationException("DB Provider is not configured.");

        _logger.Information($"Current DB Provider : {dbProvider}");

        return services
            .Configure<DatabaseSettings>(config.GetSection(nameof(DatabaseSettings)))

            .AddDbContext<WaydDbContext>(m => m.UseDatabase(dbProvider, rootConnectionString))
            .AddDomainDbContexts()

            .AddTransient<IDatabaseInitializer, DatabaseInitializer>()
            .AddTransient<ApplicationDbInitializer>()
            .AddTransient<ApplicationDbSeeder>()
            .AddTransient<ConnectionSecretBackfill>()
            .AddServices(typeof(ICustomSeeder), ServiceLifetime.Transient)
            .AddTransient<CustomSeederRunner>()

            .AddTransient<IConnectionStringSecurer, ConnectionStringSecurer>()
            .AddTransient<IConnectionStringValidator, ConnectionStringValidator>();
    }

    internal static DbContextOptionsBuilder UseDatabase(this DbContextOptionsBuilder builder, string dbProvider, string connectionString)
    {
        switch (dbProvider.ToLowerInvariant())
        {
            case DbProviderKeys.SqlServer:
                return builder.UseSqlServer(connectionString, options =>
                {
                    options.MigrationsAssembly("Wayd.Infrastructure.Migrators.MSSQL");
                    options.UseNodaTime();
                });

            //case DbProviderKeys.Npgsql:
            //    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            //    return builder.UseNpgsql(connectionString, options =>
            //    {
            //        options.MigrationsAssembly("Wayd.Infrastructure.Migrators.PostgreSQL");
            //        options.UseNodaTime();
            //    });

            default:
                throw new InvalidOperationException($"DB Provider {dbProvider} is not supported.");
        }
    }

    private static IServiceCollection AddDomainDbContexts(this IServiceCollection services)
    {
        services.AddScoped<IWaydDbContext, WaydDbContext>();
        services.AddScoped<IAppIntegrationDbContext, WaydDbContext>();
        services.AddScoped<IFeatureManagementDbContext, WaydDbContext>();
        services.AddScoped<IGoalsDbContext, WaydDbContext>();
        services.AddScoped<ILinksDbContext, WaydDbContext>();
        services.AddScoped<IOrganizationDbContext, WaydDbContext>();
        services.AddScoped<IPlanningDbContext, WaydDbContext>();
        services.AddScoped<IProjectPortfolioManagementDbContext, WaydDbContext>();
        services.AddScoped<IStrategicManagementDbContext, WaydDbContext>();
        services.AddScoped<IWorkDbContext, WaydDbContext>();

        return services;
    }
}