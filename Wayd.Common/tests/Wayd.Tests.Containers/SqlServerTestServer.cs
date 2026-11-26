using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Wayd.Tests.Containers;

/// <summary>
/// A lease on the SQL Server a test process runs against: the shared server <see cref="SharedServerVariable"/> names,
/// or else a container the process's collections share. Dispose the lease when done with the server.
/// </summary>
/// <remarks>
/// Collections run in parallel, and each takes a lease for its own database. Without a shared server, the first
/// lease starts the container and the last one released removes it, so the collections running together share one
/// engine rather than each starting its own: every engine started spends about a minute upgrading its system
/// databases, and several starting together on a developer machine compete for its memory. A collection that starts
/// only after every lease has been released starts the container again.
/// </remarks>
public sealed class SqlServerTestServer : IAsyncDisposable
{
    /// <summary>The environment variable naming a running SQL Server to use instead of starting a container.</summary>
    public const string SharedServerVariable = "WAYD_TEST_SQLSERVER";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static MsSqlContainer? _container;
    private static int _leases;

    private readonly bool _ownsLease;
    private int _disposed;

    private SqlServerTestServer(string connectionString, bool ownsLease)
    {
        ConnectionString = connectionString;
        _ownsLease = ownsLease;
    }

    /// <summary>A connection string for the server, on <c>master</c>.</summary>
    public string ConnectionString { get; }

    /// <summary>Whether this is the shared server <see cref="SharedServerVariable"/> names, rather than a container.</summary>
    public bool IsShared => !_ownsLease;

    /// <summary>Leases the shared server, or the process's container, starting the container if none is running.</summary>
    /// <param name="cancellationToken">Cancels starting the container.</param>
    public static async Task<SqlServerTestServer> Acquire(CancellationToken cancellationToken = default)
    {
        var shared = Environment.GetEnvironmentVariable(SharedServerVariable);
        if (!string.IsNullOrWhiteSpace(shared))
            return new SqlServerTestServer(OnMaster(shared), ownsLease: false);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            _container ??= await SqlServerTestContainer.Start(cancellationToken);
            _leases++;
            return new SqlServerTestServer(OnMaster(_container.GetConnectionString()), ownsLease: true);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// The tail of the engine's log, for a fixture reporting why it could not start; on a shared server, where the
    /// run's script owns the log, a note saying so.
    /// </summary>
    /// <param name="lines">How many lines from the end to return.</param>
    public async Task<string> GetLogTail(int lines)
    {
        var container = _container;
        if (!_ownsLease || container is null)
            return "(shared server — dotnet-test-projects.sh prints its log when the run fails)";

        var (stdout, stderr) = await container.GetLogsAsync();
        return string.Join(Environment.NewLine, (stdout + stderr).Split('\n').TakeLast(lines));
    }

    /// <summary>Releases the lease, removing the container when it was the last one.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!_ownsLease || Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await Gate.WaitAsync();
        try
        {
            if (--_leases > 0 || _container is null)
                return;

            var container = _container;
            _container = null;
            await container.DisposeAsync();
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string OnMaster(string connectionString) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;
}
