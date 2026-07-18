using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>Program</c> host against a throwaway SQL Server container (the production EF
/// provider + real migrations), so a command dispatched through <c>IDispatcher</c> actually runs its
/// Wolverine-generated handler, its FluentValidation middleware, and persists through the real schema.
/// This is the one end-to-end proof that the whole pipeline executes — not just that its generated code
/// compiles (that is the in-memory <see cref="WaydApiFactory"/>).
/// </summary>
/// <remarks>Requires Docker to be running on the machine executing the tests.</remarks>
public sealed class WaydSqlServerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Pinned CU, matching the existing Organization integration fixture, so schema builds identically everywhere.
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU25-GDR2-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(SqlServerImage).Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = _container.GetConnectionString();

        // Let the host run its real InitializeDatabases() (applies migrations to the container) — that is
        // the point of using a real SQL Server here rather than the in-memory provider.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:DBProvider"] = "mssql",
                ["DatabaseSettings:ConnectionString"] = connectionString,
                ["HangfireSettings:Storage:ConnectionString"] = connectionString,
                ["SecuritySettings:LocalJwt:Secret"] = "integration-test-secret-key-please-ignore-0123456789",
            });
        });
    }
}
