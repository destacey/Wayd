using Testcontainers.MsSql;

namespace Wayd.Tests.Containers;

/// <summary>Starts the SQL Server container an integration suite runs against.</summary>
public static class SqlServerTestContainer
{
    /// <summary>Builds and starts a container from <see cref="SqlServerTestImage"/>. The caller disposes it.</summary>
    public static async Task<MsSqlContainer> Start(CancellationToken cancellationToken = default)
    {
        var container = new MsSqlBuilder(SqlServerTestImage.Name).Build();
        await container.StartAsync(cancellationToken);
        return container;
    }
}
