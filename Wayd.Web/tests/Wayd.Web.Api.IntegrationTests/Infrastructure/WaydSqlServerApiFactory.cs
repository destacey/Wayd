using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Auth.OAuth;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Tests.Containers;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>Program</c> host against a throwaway SQL Server database (the production EF
/// provider + real migrations), so a command dispatched through <c>IDispatcher</c> actually runs its
/// Wolverine-generated handler, its FluentValidation middleware, and persists through the real schema.
/// This is the one end-to-end proof that the whole pipeline executes — not just that its generated code
/// compiles (that is the in-memory <see cref="WaydApiFactory"/>).
/// <para>
/// Shared as a collection fixture (see <see cref="SqlServerApiTestCollection"/>): one database, schema and
/// host for every SQL-backed class. Tests therefore share a database and must scope their assertions to
/// data they created rather than assuming they are alone in it.
/// </para>
/// </summary>
/// <remarks>Requires Docker to be running on the machine executing the tests.</remarks>
public sealed class WaydSqlServerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Long enough that a run the queue finishes is answered 200 however loaded the runner is. A test that
    // needs the wait to run out holds the run back with ImportClaims and shortens it with WaitOnImportsFor.
    private static readonly ImportResponseTiming _defaultImportResponseTiming =
        new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(250));

    private SqlServerTestDatabase _database = null!;

    private string _connectionString = null!;

    /// <summary>Connection string for the database of its own the host runs against.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>Forces a concurrency conflict on a chosen save; inert unless a test arms it.</summary>
    public ConcurrentWriteInjector ConcurrentWrites { get; }

    /// <summary>Holds import runs at Queued; inert unless a test holds it.</summary>
    public ImportClaimGate ImportClaims { get; } = new();

    /// <summary>Records what every save adds; inert unless a test is recording.</summary>
    public SaveRecorder Saves { get; } = new();

    /// <summary>Fails a chosen save; inert unless a test arms it.</summary>
    public SaveFaultInjector SaveFaults { get; } = new();

    /// <summary>The OAuth clients the authorization server can resolve, in place of fetching their metadata documents.</summary>
    public FakeOAuthClients OAuthClients { get; } = new();

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
        // A database of its own rather than master: Wolverine's Weasel envelope-table provisioning targets an
        // application database, so on master the durable-outbox schema is never provisioned.
        _database = await SqlServerTestDatabase.Create("api");
        _connectionString = _database.ConnectionString;

        // One answered query is not a warm engine. Require several in a row against the application database
        // before the host's migrations, Wolverine's message store and Hangfire's schema all hit it at once.
        for (var success = 0; success < 3; success++)
        {
            await RetryTransient("reach the application database", _connectionString, "SELECT 1;");
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        await BootHost();
        await AuthenticatedClientExtensions.CreateHarnessAdministrator(Services, CancellationToken.None);
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
        if (_database is not null)
            await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Let the host run its real InitializeDatabases() (applies migrations to the database) — that is
        // the point of using a real SQL Server here rather than the in-memory provider.
        builder.UseEnvironment("Development");

        // Inject the database connection string via a process ENVIRONMENT VARIABLE rather than
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
            services.ConfigureDbContext<WaydDbContext>(options => options.AddInterceptors(ConcurrentWrites, ImportClaims, Saves, SaveFaults)));

        builder.ConfigureTestServices(services =>
        {
            // Read per scope rather than captured once, so WaitOnImportsFor reaches the next request.
            services.RemoveAll<ImportResponseTiming>();
            services.AddScoped(_ => ImportResponseTiming);

            services.RemoveAll<IOAuthClientResolver>();
            services.AddSingleton<IOAuthClientResolver>(OAuthClients);
        });

        // Every test shares one caller address, so the production limits would throttle the suite.
        // A test of the limits themselves lowers them on a host of its own.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new[] { "OAuthAuthorize", "OAuthToken", "AuthSignIn", "AuthRefresh", "AuthProviders", "AuthSetup", "Imports" }
                .Select(limit => new KeyValuePair<string, string?>($"RateLimitSettings:{limit}:PermitLimit", "100000"))
                .Concat(
                [
                    new KeyValuePair<string, string?>("RateLimitSettings:PerUser:TokenLimit", "100000"),
                    new KeyValuePair<string, string?>("RateLimitSettings:PerUser:TokensPerPeriod", "100000"),
                    new KeyValuePair<string, string?>("RateLimitSettings:PerUserElevated:TokenLimit", "100000"),
                    new KeyValuePair<string, string?>("RateLimitSettings:PerUserElevated:TokensPerPeriod", "100000"),
                ])));
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

            throw new InvalidOperationException(await AppendServerLog(report), ex);
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
        throw new InvalidOperationException(await AppendServerLog(report));
    }

    private async Task<string> AppendServerLog(StringBuilder report)
    {
        var log = await _database.GetServerLogTail(40);
        return report.AppendLine().AppendLine("SQL Server log (tail):").AppendLine(log).ToString();
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

        throw new InvalidOperationException($"Could not {step} on the test database after {attempts} attempts.", lastError);
    }

    private sealed class ImportResponseTimingReset(WaydSqlServerApiFactory owner) : IDisposable
    {
        private readonly WaydSqlServerApiFactory _owner = owner;

        public void Dispose() => _owner.ImportResponseTiming = _defaultImportResponseTiming;
    }
}
