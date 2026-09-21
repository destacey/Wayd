using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace Wayd.Tests.Containers;

/// <summary>Starts the SQL Server container an integration suite runs against.</summary>
public static class SqlServerTestContainer
{
    // SQL Server's minimum. Uncapped, each engine sizes its memory from the whole host, unaware of the other
    // suites' engines starting beside it on the same runner.
    private const string MemoryLimitMb = "2048";

    private const int MaxAttempts = 3;

    /// <summary>
    /// How long to wait for the engine to accept a login.
    /// </summary>
    /// <remarks>
    /// Waiting on a real <c>SELECT 1</c> as <c>sa</c> rather than on the port is the right signal, and is what
    /// the module itself does — this replaces that default rather than extending it, only so the timeout can
    /// be set. The engine logs "ready for client connections" while it is still upgrading its system
    /// databases, and a login in that window fails with "An error occurred while evaluating the password";
    /// the image ships system databases older than its own binaries, so every first boot converts master and
    /// model, which has taken a minute here and can take longer on a loaded runner.
    /// <para>
    /// Baking the upgrade into a derived image would remove the wait rather than absorb it — see issue #881.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Builds and starts a container from <see cref="SqlServerTestImage"/>. The caller disposes it.</summary>
    /// <remarks>
    /// In CI the engine has crashed during start-up, dumping core before accepting a connection, and taken a
    /// whole suite down with it. Nothing has been written to it at that point, so a fresh container is started
    /// in its place. The connection retries in the fixtures cannot help: they only run once the engine is up.
    /// </remarks>
    public static async Task<MsSqlContainer> Start(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var container = new MsSqlBuilder(SqlServerTestImage.Name)
                .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", MemoryLimitMb)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted(
                        ["/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", "localhost", "-U", "sa",
                            "-P", MsSqlBuilder.DefaultPassword, "-Q", "SELECT 1;"],
                        o => o.WithTimeout(StartupTimeout)))
                .Build();

            try
            {
                await container.StartAsync(cancellationToken);
                return container;
            }
            catch (ContainerNotRunningException) when (attempt < MaxAttempts)
            {
                await container.DisposeAsync();
            }
        }
    }
}
