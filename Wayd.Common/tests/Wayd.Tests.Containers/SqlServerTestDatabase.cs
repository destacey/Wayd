using Microsoft.Data.SqlClient;

namespace Wayd.Tests.Containers;

/// <summary>
/// An empty database of a collection's own, on the server <see cref="SqlServerTestServer"/> leases, dropped on dispose.
/// </summary>
/// <remarks>
/// <c>.github/scripts/dotnet-test-projects.sh integration</c> sets <see cref="SqlServerTestServer.SharedServerVariable"/>,
/// so the whole run pays the engine's first-boot upgrade once rather than once per suite (issue #881). When it is not
/// set — a single project run from an IDE — the process starts one container and every collection's database is
/// created on it, so nothing needs setting up.
/// <para>
/// Every name is unique, so suites sharing an engine never see each other's data and a database a crashed run
/// left behind never collides with the next one. Anything instance-wide — logins, server configuration — is
/// shared by every suite on the engine, so no fixture may change it.
/// </para>
/// </remarks>
public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    // Every database this type creates starts with it, which is what lets a later run find one a crashed run
    // left on a long-lived engine.
    private const string NamePrefix = "wayd-test-";

    // Older than any run, so a database this old is from a run that never got to drop it rather than one still
    // in progress beside this one.
    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromDays(1);

    private readonly SqlServerTestServer _server;

    private SqlServerTestDatabase(SqlServerTestServer server, string name)
    {
        _server = server;
        Name = name;
        ConnectionString = new SqlConnectionStringBuilder(server.ConnectionString) { InitialCatalog = name }.ConnectionString;
    }

    /// <summary>The database's name, unique to this run and suite.</summary>
    public string Name { get; }

    /// <summary>A connection string for the database.</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// A connection string for the server, on <c>master</c>, for a test that creates a database of its own.
    /// </summary>
    public string ServerConnectionString => _server.ConnectionString;

    /// <summary>
    /// Creates an empty database named for <paramref name="suite"/> on the leased server. The caller disposes it.
    /// </summary>
    /// <param name="suite">A short name for the suite, which keeps the database recognisable on a shared engine.</param>
    /// <param name="cancellationToken">Cancels starting the container and creating the database.</param>
    public static async Task<SqlServerTestDatabase> Create(string suite, CancellationToken cancellationToken = default)
    {
        var name = $"{NamePrefix}{suite.ToLowerInvariant()}-{Guid.NewGuid():N}";
        var server = await SqlServerTestServer.Acquire(cancellationToken);
        try
        {
            // Only a long-lived engine outlives the run that crashed; a container is new.
            if (server.IsShared)
                await DropAbandoned(server.ConnectionString, cancellationToken);
            await CreateDatabase(server.ConnectionString, name, cancellationToken);
            return new SqlServerTestDatabase(server, name);
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// The tail of the engine's log, for a fixture reporting why it could not start; on a shared server, where the
    /// run's script owns the log, a note saying so.
    /// </summary>
    /// <param name="lines">How many lines from the end to return.</param>
    public Task<string> GetServerLogTail(int lines) => _server.GetLogTail(lines);

    /// <summary>
    /// Takes a snapshot of the database as it is now, for <see cref="RevertToSnapshot"/> to return it to. A database
    /// has at most one; it is dropped with the database.
    /// </summary>
    /// <param name="cancellationToken">Cancels creating the snapshot.</param>
    public async Task CreateSnapshot(CancellationToken cancellationToken = default)
    {
        string logicalName, dataFile;
        await using (var connection = new SqlConnection(_server.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name, physical_name FROM sys.master_files WHERE database_id = DB_ID(@name) AND type = 0;";
            command.Parameters.AddWithValue("@name", Name);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException($"Database {Name} has no data file to snapshot.");
            (logicalName, dataFile) = (reader.GetString(0), reader.GetString(1));
        }

        await Execute(_server.ConnectionString,
            $"CREATE DATABASE {Quote(SnapshotName)} ON (NAME = {Quote(logicalName)}, FILENAME = {Literal(dataFile + ".ss")}) AS SNAPSHOT OF {Quote(Name)};",
            cancellationToken);
    }

    /// <summary>
    /// Returns the database to its snapshot, undoing everything written since, schema included. It takes a
    /// fraction of a second, where building the same database again means migrating it from nothing.
    /// </summary>
    /// <remarks>
    /// The revert needs the database to itself, so it disconnects every session on it; a pooled connection to it is
    /// broken afterwards, so the pool is cleared too.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the revert.</param>
    public async Task RevertToSnapshot(CancellationToken cancellationToken = default)
    {
        await Execute(_server.ConnectionString, $"ALTER DATABASE {Quote(Name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", cancellationToken);
        try
        {
            await Execute(_server.ConnectionString,
                $"RESTORE DATABASE {Quote(Name)} FROM DATABASE_SNAPSHOT = {Literal(SnapshotName)};", cancellationToken);
        }
        finally
        {
            // Left in single-user mode, every later test would fail on that rather than on whatever stopped the revert.
            await Execute(_server.ConnectionString, $"ALTER DATABASE {Quote(Name)} SET MULTI_USER;", CancellationToken.None);
        }

        using var pooled = new SqlConnection(ConnectionString);
        SqlConnection.ClearPool(pooled);
    }

    /// <summary>The name of the database's snapshot, which the sweep's prefix also matches.</summary>
    private string SnapshotName => $"{Name}-snapshot";

    /// <summary>Drops the database and its snapshot, and releases the server lease.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // A pooled connection to the database keeps a session open on it, and DROP refuses a database in use.
            // Only idle connections are cleared, so another collection's work in progress is untouched.
            SqlConnection.ClearAllPools();
            await Drop(_server.ConnectionString, SnapshotName, CancellationToken.None);
            await Drop(_server.ConnectionString, Name, CancellationToken.None);
        }
        catch (SqlException)
        {
            // Every test has already run, so a failed drop must not fail the suite. The name is unique, so the
            // database left behind collides with nothing, and the sweep in a later run drops it.
        }
        finally
        {
            await _server.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates the database, retrying while the engine refuses for reasons that pass on their own.
    /// </summary>
    /// <remarks>
    /// <c>CREATE DATABASE</c> copies <c>model</c> and needs an exclusive lock on it, so two suites creating
    /// their databases on one engine at the same moment can refuse each other (error 1807). On a container that
    /// has just started, <c>model</c> can also still be upgrading, and a login then fails with 18456.
    /// </remarks>
    private static async Task CreateDatabase(string server, string name, CancellationToken cancellationToken)
    {
        const int attempts = 10;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await Execute(server, $"IF DB_ID({Literal(name)}) IS NULL CREATE DATABASE {Quote(name)};", cancellationToken);
                return;
            }
            catch (SqlException ex) when (ex.Number is (1807 or 18456) && attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }

    /// <summary>Drops this type's databases that are too old to belong to a run still in progress.</summary>
    private static async Task DropAbandoned(string server, CancellationToken cancellationToken)
    {
        var abandoned = new List<string>();

        await using (var connection = new SqlConnection(server))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            // Snapshots first: a database that still has one cannot be dropped.
            command.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @prefix AND create_date < DATEADD(SECOND, -@age, GETDATE()) "
                + "ORDER BY CASE WHEN source_database_id IS NULL THEN 1 ELSE 0 END;";
            command.Parameters.AddWithValue("@prefix", NamePrefix.Replace("-", "[-]", StringComparison.Ordinal) + "%");
            command.Parameters.AddWithValue("@age", (int)AbandonedAfter.TotalSeconds);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                abandoned.Add(reader.GetString(0));
        }

        foreach (var name in abandoned)
        {
            try
            {
                await Drop(server, name, cancellationToken);
            }
            catch (SqlException)
            {
                // Another run's sweep got to it first, or something still holds it; either way it is not this
                // suite's database, and the next sweep tries again.
            }
        }
    }

    /// <summary>Drops a database or a snapshot, disconnecting whatever is still connected to it.</summary>
    /// <remarks>A snapshot is read-only and refuses <c>SINGLE_USER</c>; dropping it disconnects its sessions anyway.</remarks>
    private static Task Drop(string server, string name, CancellationToken cancellationToken) =>
        Execute(server, $"""
            IF DB_ID({Literal(name)}) IS NOT NULL
            BEGIN
                IF (SELECT source_database_id FROM sys.databases WHERE name = {Literal(name)}) IS NULL
                    ALTER DATABASE {Quote(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE {Quote(name)};
            END
            """, cancellationToken);

    /// <summary>Runs one statement on a connection of its own.</summary>
    private static async Task Execute(string connectionString, string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Bracket-quotes a database name, which cannot be passed as a parameter.</summary>
    private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    /// <summary>Writes a database name as a Unicode string literal, for <c>DB_ID</c>.</summary>
    private static string Literal(string identifier) => $"N'{identifier.Replace("'", "''", StringComparison.Ordinal)}'";
}
