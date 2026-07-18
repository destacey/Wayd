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
using Wolverine.EntityFrameworkCore;
using Serilog;

namespace Wayd.Infrastructure.Persistence;

internal static class ConfigureServices
{
    private static readonly ILogger _logger = Log.ForContext(typeof(ConfigureServices));

    /// <summary>
    /// Dedicated schema for Wolverine's durable inbox/outbox envelope tables. Deliberately NOT
    /// <c>dbo</c>: these tables are provisioned by Weasel at startup (parallel to our EF migrations),
    /// so isolating them keeps them out of the application schema and out of our migration history.
    /// Shared with <see cref="Messaging.WolverineConfiguration"/>, which points
    /// <c>PersistMessagesWithSqlServer</c> at the same schema.
    /// </summary>
    internal const string WolverineSchemaName = "wolverine";

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

        services.Configure<DatabaseSettings>(config.GetSection(nameof(DatabaseSettings)));

        // Register WaydDbContext WITH Wolverine's EF Core integration instead of a plain AddDbContext.
        // This is the persistence half of the durable transactional outbox (the messaging half —
        // PersistMessagesWithSqlServer + UseEntityFrameworkCoreTransactions — is wired in
        // WolverineConfiguration). It swaps in Wolverine's IModelCustomizer so the outbox/inbox envelope
        // tables are known to the EF model, and registers IDbContextOutbox so a future async event can
        // enlist in the SaveChanges transaction and commit atomically with its entity changes.
        //
        // Behaviour today is unchanged: no domain event is routed durably yet — every event is an inline
        // cross-domain replication projection that must preserve read-your-writes — so EventPublisher
        // still dispatches inline via InvokeAsync. The envelope tables live in a dedicated "wolverine"
        // schema (never dbo) and are provisioned by Weasel at startup, parallel to our EF migrations, not
        // inside them. The DbContext stays Scoped exactly as before; only DbContextOptions becomes
        // Singleton (a Wolverine optimisation) — safe because UseDatabase closes over config strings only.
        services.AddDbContextWithWolverineIntegration<WaydDbContext>(
            m => m.UseDatabase(dbProvider, rootConnectionString),
            wolverineDatabaseSchema: WolverineSchemaName);

        return services
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