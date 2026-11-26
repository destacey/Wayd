using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Wayd.Tests.Containers;

/// <summary>
/// An empty database of a suite's own, on the run's shared SQL Server or on a container started for the suite.
/// </summary>
/// <remarks>
/// When <see cref="SharedServerVariable"/> holds a connection string, the database is created on that server
/// and dropped on dispose; <c>.github/scripts/dotnet-test-projects.sh integration</c> sets it, so the whole run
/// pays the engine's first-boot upgrade once rather than once per suite (issue #881). When it is not set — a
/// single project run from an IDE — a container is started with <see cref="SqlServerTestContainer"/> and the
/// database is created on it, so nothing needs setting up.
/// <para>
/// Every name is unique, so suites sharing an engine never see each other's data and a database a crashed run
/// left behind never collides with the next one. Anything instance-wide — logins, server configuration — is
/// shared by every suite on the engine, so no fixture may change it.
/// </para>
/// </remarks>
public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    /// <summary>The environment variable naming a running SQL Server to create the database on.</summary>
    public const string SharedServerVariable = "WAYD_TEST_SQLSERVER";

    // Every database this type creates starts with it, which is what lets a later run find one a crashed run
    // left on a long-lived engine.
    private const string NamePrefix = "wayd-test-";

    // Older than any run, so a database this old is from a run that never got to drop it rather than one still
    // in progress beside this one.
    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromDays(1);

    private readonly MsSqlContainer? _container;
    private readonly string _serverConnectionString;

    private SqlServerTestDatabase(MsSqlContainer? container, string serverConnectionString, string name)
    {
        _container = container;
        _serverConnectionString = serverConnectionString;
        Name = name;
        ConnectionString = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = name }.ConnectionString;
    }

    /// <summary>The database's name, unique to this run and suite.</summary>
    public string Name { get; }

    /// <summary>A connection string for the database.</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// A connection string for the server, on <c>master</c>, for a test that creates a database of its own.
    /// </summary>
    public string ServerConnectionString => _serverConnectionString;

    /// <summary>
    /// Creates an empty database named for <paramref name="suite"/>, on the shared server when there is one and on a
    /// new container otherwise. The caller disposes it.
    /// </summary>
    /// <param name="suite">A short name for the suite, which keeps the database recognisable on a shared engine.</param>
    /// <param name="cancellationToken">Cancels starting the container and creating the database.</param>
    public static async Task<SqlServerTestDatabase> Create(string suite, CancellationToken cancellationToken = default)
    {
        var name = $"{NamePrefix}{suite.ToLowerInvariant()}-{Guid.NewGuid():N}";
        var shared = Environment.GetEnvironmentVariable(SharedServerVariable);

        if (!string.IsNullOrWhiteSpace(shared))
        {
            var server = new SqlConnectionStringBuilder(shared) { InitialCatalog = "master" }.ConnectionString;
            await DropAbandoned(server, cancellationToken);
            await CreateDatabase(server, name, cancellationToken);
            return new SqlServerTestDatabase(null, server, name);
        }

        var container = await SqlServerTestContainer.Start(cancellationToken);
        try
        {
            var server = container.GetConnectionString();
            await CreateDatabase(server, name, cancellationToken);
            return new SqlServerTestDatabase(container, server, name);
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// The tail of the engine's log, for a fixture reporting why it could not start; on a shared server, where the
    /// run's script owns the log, a note saying so.
    /// </summary>
    /// <param name="lines">How many lines from the end to return.</param>
    public async Task<string> GetServerLogTail(int lines)
    {
        if (_container is null)
            return "(shared server — dotnet-test-projects.sh prints its log when the run fails)";

        var (stdout, stderr) = await _container.GetLogsAsync();
        return string.Join(Environment.NewLine, (stdout + stderr).Split('\n').TakeLast(lines));
    }

    /// <summary>Drops the database on a shared server, or removes the container the database lives on.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        // A pooled connection to the database keeps a session open on it, and DROP refuses a database in use.
        SqlConnection.ClearAllPools();
        try
        {
            await Drop(_serverConnectionString, Name, CancellationToken.None);
        }
        catch (SqlException)
        {
            // Every test has already run, so a failed drop must not fail the suite. The name is unique, so the
            // database left behind collides with nothing, and the sweep in a later run drops it.
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
            command.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @prefix AND create_date < DATEADD(SECOND, -@age, GETDATE());";
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

    /// <summary>Drops a database, disconnecting whatever is still connected to it.</summary>
    private static Task Drop(string server, string name, CancellationToken cancellationToken) =>
        Execute(server, $"""
            IF DB_ID({Literal(name)}) IS NOT NULL
            BEGIN
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
