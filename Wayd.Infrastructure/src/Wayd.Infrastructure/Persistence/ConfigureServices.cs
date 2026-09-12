using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Links;
using Wayd.Planning.Application.Persistence;
using Wayd.ProductManagement.Application;
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

    /// <summary>
    /// Points every module's interface at the one <see cref="WaydDbContext"/> in the scope.
    /// </summary>
    /// <remarks>
    /// A factory rather than <c>AddScoped&lt;IModuleDbContext, WaydDbContext&gt;()</c>, which reads as an
    /// alias but is not one: each of those is its own service descriptor, so every interface handed out a
    /// separate context with a separate change tracker. Anything spanning two of them silently half-worked.
    /// <para>
    /// The import runner is where it surfaced. A definition adds records through its module's interface
    /// while the runner saves through <c>IImportDbContext</c>, so the save persisted the row outcomes and
    /// discarded everything the import had created — a run reporting every row applied and creating none.
    /// </para>
    /// <para>
    /// A factory is opaque to Wolverine's codegen, so each of these is also allow-listed for service
    /// location in <c>WolverineConfiguration</c>. Both halves are required: without the allow-list codegen
    /// cannot see through the registration, and without the factory it inline-constructs a private context
    /// per interface and never consults the container at all.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddDomainDbContexts(this IServiceCollection services)
    {
        services.AddScoped<IWaydDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IAppIntegrationDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IFeatureManagementDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IImportDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<ILinksDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IOrganizationDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IPlanningDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IProductManagementDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IStatusWorkflowDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IActivityLogDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IProjectPortfolioManagementDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IStrategicManagementDbContext>(sp => sp.GetRequiredService<WaydDbContext>());
        services.AddScoped<IWorkDbContext>(sp => sp.GetRequiredService<WaydDbContext>());

        return services;
    }
}