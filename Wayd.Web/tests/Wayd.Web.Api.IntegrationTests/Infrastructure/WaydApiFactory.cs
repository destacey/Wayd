using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayd.Infrastructure.Persistence.Context;
using Wolverine;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>Program</c> host (so Wolverine actually initializes and generates handler code)
/// with the SQL Server <see cref="WaydDbContext"/> swapped for the EF in-memory provider. Used by the
/// Wolverine configuration-validity check, which only needs the container to build and the generated
/// code to compile — it never queries the database — so no SQL Server / Docker is required.
/// </summary>
public sealed class WaydApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Never touch a real database on boot: WAYD_SKIP_DB_INIT stops InitializeDatabases()/bootstrap,
        // and a placeholder connection string keeps AddPersistence's config validation from throwing.
        // Set it as a host setting only (not a process-wide env var) so it cannot leak into other
        // factories running in the same test process.
        builder.UseEnvironment("Development");
        builder.UseSetting("WAYD_SKIP_DB_INIT", "true");

        // Force Static codegen so the integration suite boots the SAME pre-generated handler tree the shipped
        // artifact runs — the whole point of this guard is to exercise prod's dispatch path, not the Auto/Roslyn
        // path only local dev uses. Development environment otherwise picks up appsettings.Development.json's
        // Wolverine:CodegenMode=Auto; this override wins. The freshly generated tree must exist on disk (CI runs
        // `codegen write` before `dotnet test`); a plain local `dotnet test` needs it regenerated first too.
        builder.UseSetting("Wolverine:CodegenMode", "Static");

        // Disable Wolverine's SQL durable outbox for this in-memory factory. With it registered,
        // WolverineRuntime.StartAsync → tryMigrateStorage() opens a SqlConnection and migrates the message store
        // against DatabaseSettings:ConnectionString at host start; with no reachable DB that connect times out and
        // the failed startup tears the host down (every test then fails with ObjectDisposedException at
        // CreateClient). The flag makes WolverineConfiguration register NO SQL store (MediatorOnly), so there is
        // nothing to migrate — leaving discovery + codegen + dispatch, all this guard checks. The Testcontainers
        // WaydSqlServerApiFactory leaves this false and provisions the real outbox against its container.
        builder.UseSetting("Wolverine:DisableDurableOutbox", "true");

        // Inject the placeholder settings as process ENVIRONMENT VARIABLES, not via ConfigureAppConfiguration.
        // Confirmed via diagnostics: AddWaydWolverine reads DatabaseSettings:ConnectionString SYNCHRONOUSLY during
        // AddInfrastructure (and passes it to PersistMessagesWithSqlServer, which parses it) BEFORE the host is
        // built — so a deferred ConfigureAppConfiguration override is NOT visible there, and neither is UseSetting
        // when a *.json file also sets the key. The eager read otherwise resolves to user-secrets locally or
        // database.json's `(localdb)\mssqllocaldb` in CI; `(localdb)` throws PlatformNotSupportedException at parse
        // time on Linux, killing the host boot (invisible on Windows). AddConfigurations() ends with
        // AddEnvironmentVariables() (highest precedence, applied immediately), the one source that both reaches the
        // eager read and out-ranks the json/user-secrets fallbacks. The value just needs to PARSE everywhere; it is
        // never connected to (in-memory EF + in-memory Hangfire + DisableDurableOutbox mean nothing opens a
        // connection). Mirrors WaydSqlServerApiFactory; `__` is the section separator. Cleared in DisposeAsync to
        // avoid leaking into sibling factories (which set the same keys) — safe because xunit.runner.json disables
        // collection parallelism.
        const string placeholderConnectionString =
            "Server=127.0.0.1,1433;Database=WaydTest;User Id=sa;Password=Placeholder_not_used_1;TrustServerCertificate=true;Connect Timeout=1";
        Environment.SetEnvironmentVariable("DatabaseSettings__DBProvider", "mssql");
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", placeholderConnectionString);
        Environment.SetEnvironmentVariable("HangfireSettings__Storage__ConnectionString", placeholderConnectionString);
        Environment.SetEnvironmentVariable("SecuritySettings__LocalJwt__Secret", "integration-test-secret-key-please-ignore-0123456789");

        builder.ConfigureTestServices(services =>
        {
            // Wolverine's documented convention for a host with no real database: solo mode skips the durable
            // inbox/outbox agents and leader election (their background recovery loops would otherwise run
            // against the swapped-out storage). See https://wolverinefx.io/guide/http/integration-testing.html
            services.RunWolverineInSoloMode();

            // Replace the SQL Server WaydDbContext with the in-memory provider so nothing connects.
            // Both AddDbContext calls register their provider into the app service provider, so give the
            // in-memory context its own internal service provider to avoid the "multiple database
            // providers registered" conflict.
            //
            // Known limitation: an external service provider is frozen from EF's perspective, so ASP.NET
            // Identity's ReplaceService call throws InvalidOperationException the moment anything resolves
            // the Identity stack — surfacing as a 500 from the exception middleware, before the endpoint's
            // own auth or handler logic runs. Identity-backed endpoints (all of /api/auth included) cannot
            // be exercised through this factory; use WaydSqlServerApiFactory for those. Dropping
            // UseInternalServiceProvider does not fix it — AddDbContextWithWolverineIntegration re-registers
            // the SQL Server provider past RemoveAll, and the provider conflict returns instead.
            services.RemoveAll(typeof(DbContextOptions<WaydDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll<WaydDbContext>();

            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<WaydDbContext>(options => options
                .UseInMemoryDatabase("wolverine-config-check")
                .UseInternalServiceProvider(inMemoryProvider));

            // Swap Hangfire's SQL Server storage for in-memory. SqlServerStorage.Initialize() runs a schema
            // migration against the configured connection string when the JobStorage singleton is constructed at
            // startup — this factory points at an unreachable placeholder DB, so that connect blocks for ~20s and
            // the failed startup tears the host down. In-memory storage keeps the Hangfire graph intact (the
            // dashboard middleware and server still resolve) without any database. The Testcontainers
            // WaydSqlServerApiFactory keeps SQL storage for the real end-to-end pipeline tests.
            services.RemoveAll<JobStorage>();
            services.AddHangfire(config => config.UseInMemoryStorage());
        });
    }

    public override async ValueTask DisposeAsync()
    {
        // Clear the process-global env vars set in ConfigureWebHost so they cannot leak into sibling factories
        // (notably WaydSqlServerApiFactory, which sets the SAME keys to its real container) later in the same
        // test process. Safe because xunit.runner.json disables collection parallelism.
        Environment.SetEnvironmentVariable("DatabaseSettings__DBProvider", null);
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", null);
        Environment.SetEnvironmentVariable("HangfireSettings__Storage__ConnectionString", null);
        Environment.SetEnvironmentVariable("SecuritySettings__LocalJwt__Secret", null);

        await base.DisposeAsync();
    }
}
