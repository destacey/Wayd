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
