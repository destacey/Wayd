using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Persistence.Initialization;

internal class DatabaseInitializer(
    WaydDbContext context,
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    /// <summary>
    /// How long a single migration command may run. Data migrations that reconstruct history from the
    /// audit trail read every row of a table that grows without bound, which comfortably outlives the
    /// 30-second ADO.NET default. Applied only while migrations run, then restored, so no runtime query
    /// inherits it — a slow query at runtime should still fail fast.
    /// </summary>
    private static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromSeconds(180);

    private readonly WaydDbContext _context = context;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly bool _isDevelopment = environment.IsDevelopment();
    private readonly ILogger<DatabaseInitializer> _logger = logger;

    public async Task InitializeDatabase(CancellationToken cancellationToken)
    {
        // After the migrations, which are what create the database when it does not exist yet. Asking
        // sys.databases about a catalog that has not been created cannot answer, so running this first
        // meant a first boot warned and moved on with row versioning never turned on — silently skipping
        // the one case it is for.
        await InitializeDb(cancellationToken);
        await EnsureReadCommittedSnapshot(cancellationToken);
        await InitializeApplicationDb(cancellationToken);
    }

    /// <summary>
    /// Makes sure the database reads under row versioning rather than blocking on writers.
    /// </summary>
    /// <remarks>
    /// Wayd expects <c>READ_COMMITTED_SNAPSHOT</c>. A save commits its records together with the audit trail,
    /// activity log and outbox envelopes that record them, which is a longer transaction than the entity
    /// write alone; without row versioning a reader blocks behind it, and polling an import's status times
    /// out while the import is doing exactly what it should.
    /// <para>
    /// Azure SQL Database has it on for every new database, so there it is already true and this does
    /// nothing. SQL Server and Azure SQL Managed Instance default it off, which is why a local container
    /// behaves differently from the deployed article unless something turns it on.
    /// </para>
    /// <para>
    /// Only switched on automatically in Development: the change needs exclusive access, so it disconnects
    /// whoever else is on the database. Anywhere else that is the operator's call to make in a window of
    /// their choosing, and all this does is say so.
    /// </para>
    /// </remarks>
    private async Task EnsureReadCommittedSnapshot(CancellationToken cancellationToken)
    {
        // sys.databases and the ALTER below are SQL Server's; another provider needs its own answer.
        if (!_context.Database.IsSqlServer())
            return;

        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        try
        {
            var alreadyOn = await _context.Database
                .SqlQuery<bool>($"SELECT is_read_committed_snapshot_on AS Value FROM sys.databases WHERE name = DB_NAME()")
                .SingleOrDefaultAsync(cancellationToken);

            if (alreadyOn)
                return;

            if (!_isDevelopment)
            {
                _logger.LogWarning(
                    "Database {Database} has READ_COMMITTED_SNAPSHOT off. Wayd expects it on, as Azure SQL Database "
                    + "has it by default; with it off, readers block behind the transaction a save holds and status "
                    + "polling can time out. Enable it during a maintenance window: "
                    + "ALTER DATABASE [{Database}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;",
                    databaseName, databaseName);

                return;
            }

            await EnableReadCommittedSnapshot(connectionString, databaseName, cancellationToken);

            _logger.LogInformation("Enabled READ_COMMITTED_SNAPSHOT on {Database}.", databaseName);
        }
        catch (Exception exception)
        {
            // Never a reason not to start. The application runs correctly without row versioning — readers
            // block behind a save's transaction rather than seeing its prior snapshot — so a database whose
            // login cannot ALTER it, or an engine that refuses while another session holds it, gets a warning
            // and a working application rather than a failed boot.
            _logger.LogWarning(
                exception,
                "Could not confirm READ_COMMITTED_SNAPSHOT on {Database}. Continuing without it; see the warning "
                + "above for what to run by hand.",
                databaseName);
        }
    }

    /// <summary>
    /// Runs the ALTER from <c>master</c> rather than through the context.
    /// </summary>
    /// <remarks>
    /// Two reasons it cannot go through <c>_context</c>. The statement takes exclusive access and
    /// <c>ROLLBACK IMMEDIATE</c> disconnects every other session on the database — including the context's own
    /// pooled connections, which is the boot it is running inside. And a connection to the database being
    /// altered is itself one of the sessions in the way.
    /// </remarks>
    private static async Task EnableReadCommittedSnapshot(
        string connectionString, string databaseName, CancellationToken cancellationToken)
    {
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        // A database name cannot be a parameter, so it is bracket-quoted; it comes from the connection string
        // the application is already using, not from anything a request supplies.
        command.CommandText = $"ALTER DATABASE {Quote(databaseName)} SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    public async Task InitializeApplicationDb(CancellationToken cancellationToken)
    {
        // First create a new scope
        using var scope = _serviceProvider.CreateScope();

        // Then run the initialization in the new scope
        await scope.ServiceProvider.GetRequiredService<ApplicationDbInitializer>()
            .Initialize(cancellationToken);
    }

    private async Task InitializeDb(CancellationToken cancellationToken)
    {
        if (_context.Database.GetPendingMigrations().Any())
        {
            _logger.LogInformation("Applying Root Migrations.");

            using var commandTimeout = _context.WithCommandTimeout(MigrationCommandTimeout);

            await _context.Database.MigrateAsync(cancellationToken);
        }
    }
}