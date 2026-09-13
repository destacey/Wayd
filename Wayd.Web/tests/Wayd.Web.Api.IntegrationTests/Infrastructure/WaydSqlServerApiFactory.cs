using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>Program</c> host against a throwaway SQL Server container (the production EF
/// provider + real migrations), so a command dispatched through <c>IDispatcher</c> actually runs its
/// Wolverine-generated handler, its FluentValidation middleware, and persists through the real schema.
/// This is the one end-to-end proof that the whole pipeline executes — not just that its generated code
/// compiles (that is the in-memory <see cref="WaydApiFactory"/>).
/// <para>
/// Shared as a collection fixture (see <see cref="SqlServerApiTestCollection"/>): one container, schema and
/// host for every SQL-backed class. Tests therefore share a database and must scope their assertions to
/// data they created rather than assuming they are alone in it.
/// </para>
/// </summary>
/// <remarks>Requires Docker to be running on the machine executing the tests.</remarks>
public sealed class WaydSqlServerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Pinned CU, matching the existing Organization integration fixture, so schema builds identically everywhere.
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2025-CU8-ubuntu-24.04";

    // A dedicated application database (not the container's default `master`). Production never runs on
    // master, and Wolverine's Weasel envelope-table provisioning targets a real application database — so
    // the integration host must too, or the durable-outbox schema is never provisioned.
    private const string DatabaseName = "WaydIntegrationTests";

    // Long enough that a run the queue finishes is answered 200 however loaded the runner is. A test that
    // needs the wait to run out holds the run back with ImportClaims and shortens it with WaitOnImportsFor.
    private static readonly ImportResponseTiming _defaultImportResponseTiming =
        new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(250));

    private readonly MsSqlContainer _container = new MsSqlBuilder(SqlServerImage).Build();

    private string _connectionString = null!;

    /// <summary>Connection string for the dedicated container database the host runs against.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>Forces a concurrency conflict on a chosen save; inert unless a test arms it.</summary>
    public ConcurrentWriteInjector ConcurrentWrites { get; }

    /// <summary>Holds import runs at Queued; inert unless a test holds it.</summary>
    public ImportClaimGate ImportClaims { get; } = new();

    /// <summary>How long an import submission waits on its run before answering 202.</summary>
    public ImportResponseTiming ImportResponseTiming { get; private set; } = _defaultImportResponseTiming;

    public WaydSqlServerApiFactory()
    {
        ConcurrentWrites = new ConcurrentWriteInjector(() => _connectionString);
    }

    /// <summary>Shortens the import response wait until the returned handle is disposed.</summary>
    public IDisposable WaitOnImportsFor(TimeSpan budget)
    {
        ImportResponseTiming = _defaultImportResponseTiming with { Budget = budget };
        return new ImportResponseTimingReset(this);
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Testcontainers reports the container ready as soon as SQL Server accepts a connection, but under CI
        // load (several of these containers starting at once on far fewer cores) the engine can still be
        // warming up and drop or time out the next commands. Both steps are retried for that reason.
        await RetryTransient(
            "create the application database",
            _container.GetConnectionString(),
            $"IF DB_ID('{DatabaseName}') IS NULL CREATE DATABASE [{DatabaseName}];");

        _connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
        }.ConnectionString;

        // One answered query is not a warm engine. Require several in a row against the application database
        // before the host's migrations, Wolverine's message store and Hangfire's schema all hit it at once.
        for (var success = 0; success < 3; success++)
        {
            await RetryTransient("reach the application database", _connectionString, "SELECT 1;");
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        await BootHost();
    }

    public override async ValueTask DisposeAsync()
    {
        // Clear the env vars this factory set so nothing leaks to sibling test hosts. Safe because this
        // factory is a single collection fixture (see SqlServerApiTestCollection) and xunit.runner.json
        // disables collection parallelism, so no other host is constructing while these are cleared.
        Environment.SetEnvironmentVariable("DatabaseSettings__DBProvider", null);
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", null);
        Environment.SetEnvironmentVariable("HangfireSettings__Storage__ConnectionString", null);
        Environment.SetEnvironmentVariable("SecuritySettings__LocalJwt__Secret", null);
        HandlerCodegenMode.Clear();

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
        HandlerCodegenMode.Apply();

        builder.ConfigureServices(services =>
            services.ConfigureDbContext<WaydDbContext>(options => options.AddInterceptors(ConcurrentWrites, ImportClaims)));

        builder.ConfigureTestServices(services =>
        {
            // Read per scope rather than captured once, so WaitOnImportsFor reaches the next request.
            services.RemoveAll<ImportResponseTiming>();
            services.AddScoped(_ => ImportResponseTiming);
        });
    }

    /// <summary>
    /// Starts the host here, so a failed start fails the fixture with its cause rather than surfacing in
    /// whichever test first touches the host.
    /// </summary>
    /// <remarks>
    /// WebApplicationFactory disposes a host whose start threw and then reports only the
    /// <see cref="ObjectDisposedException"/> from touching it, so the cause is recorded as it is thrown.
    /// </remarks>
    private async Task BootHost()
    {
        var thrown = new ConcurrentQueue<Exception>();
        void Record(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is ObjectDisposedException)
                return;

            thrown.Enqueue(e.Exception);
            while (thrown.Count > 10)
                thrown.TryDequeue(out _);
        }

        HttpClient client;
        AppDomain.CurrentDomain.FirstChanceException += Record;
        try
        {
            client = CreateClient();
        }
        catch (Exception ex)
        {
            AppDomain.CurrentDomain.FirstChanceException -= Record;

            var report = new StringBuilder("The API host failed to start.");
            report.AppendLine().AppendLine().AppendLine("Last exceptions thrown while it started (some may have been handled):");
            foreach (var exception in thrown)
                report.AppendLine($"- {exception.GetType().FullName}: {exception.Message}").AppendLine(exception.StackTrace);

            throw new InvalidOperationException(await AppendContainerLog(report), ex);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Record;
        }

        await WaitUntilHealthy(client);
    }

    /// <summary>
    /// Waits for the readiness endpoint, which includes the database check, so a host that started but
    /// cannot reach its database fails here rather than in the first test to query it.
    /// </summary>
    private async Task WaitUntilHealthy(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var last = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            using var response = await client.GetAsync(ServiceEndpoints.HealthEndpointPath);
            if (response.IsSuccessStatusCode)
                return;

            last = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        var report = new StringBuilder($"The API host started but {ServiceEndpoints.HealthEndpointPath} never reported healthy. Last response: {last}");
        throw new InvalidOperationException(await AppendContainerLog(report));
    }

    private async Task<string> AppendContainerLog(StringBuilder report)
    {
        var (stdout, stderr) = await _container.GetLogsAsync();
        return report.AppendLine().AppendLine("SQL Server container log (tail):").AppendLine(Tail(stdout + stderr, 40)).ToString();
    }

    private static async Task RetryTransient(string step, string connectionString, string commandText)
    {
        const int attempts = 5;
        var lastError = default(SqlException);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = commandText;
                await command.ExecuteNonQueryAsync();
                return;
            }
            catch (SqlException ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }

        throw new InvalidOperationException($"Could not {step} on the test container after {attempts} attempts.", lastError);
    }

    private static string Tail(string text, int lines) =>
        string.Join(Environment.NewLine, text.Split('\n').TakeLast(lines));

    private sealed class ImportResponseTimingReset(WaydSqlServerApiFactory owner) : IDisposable
    {
        private readonly WaydSqlServerApiFactory _owner = owner;

        public void Dispose() => _owner.ImportResponseTiming = _defaultImportResponseTiming;
    }
}
