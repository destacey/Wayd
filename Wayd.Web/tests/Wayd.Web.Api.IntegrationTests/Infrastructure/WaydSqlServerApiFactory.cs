using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
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

    // A dedicated application database (not the container's default `master`). Production never runs on
    // master, and Wolverine's Weasel envelope-table provisioning targets a real application database — so
    // the integration host must too, or the durable-outbox schema is never provisioned.
    private const string DatabaseName = "WaydIntegrationTests";

    private string _connectionString = null!;

    /// <summary>Connection string for the dedicated container database the host runs against.</summary>
    public string ConnectionString => _connectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Create the dedicated database, then point the host's connection string at it.
        await using (var connection = new SqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID('{DatabaseName}') IS NULL CREATE DATABASE [{DatabaseName}];";
            await command.ExecuteNonQueryAsync();
        }

        _connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
        }.ConnectionString;
    }

    public override async ValueTask DisposeAsync()
    {
        // Clear the env vars this factory set so nothing leaks to sibling test hosts. Safe because
        // xunit.runner.json disables collection parallelism (these are process-global vars).
        Environment.SetEnvironmentVariable("DatabaseSettings__DBProvider", null);
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", null);
        Environment.SetEnvironmentVariable("HangfireSettings__Storage__ConnectionString", null);
        Environment.SetEnvironmentVariable("SecuritySettings__LocalJwt__Secret", null);

        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Let the host run its real InitializeDatabases() (applies migrations to the container) — that is
        // the point of using a real SQL Server here rather than the in-memory provider.
        builder.UseEnvironment("Development");

        // Inject the container connection string via a process ENVIRONMENT VARIABLE rather than
        // ConfigureAppConfiguration. This is the one config source the host reads SYNCHRONOUSLY and early
        // enough: Program.cs configures the Wolverine durable outbox during host construction, and
        // PersistMessagesWithSqlServer needs the connection string right then — before any deferred
        // ConfigureAppConfiguration override applies. The app's AddConfigurations() ends with
        // AddEnvironmentVariables() (highest precedence, applied immediately), so an env var both reaches
        // that eager read and out-ranks the database.json fallback. The double underscore is the .NET
        // section separator. Cleared in DisposeAsync; safe because collection parallelism is disabled.
        Environment.SetEnvironmentVariable("DatabaseSettings__DBProvider", "mssql");
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", _connectionString);
        Environment.SetEnvironmentVariable("HangfireSettings__Storage__ConnectionString", _connectionString);
        Environment.SetEnvironmentVariable("SecuritySettings__LocalJwt__Secret", "integration-test-secret-key-please-ignore-0123456789");
    }
}
