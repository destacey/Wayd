using Hangfire;
using Hangfire.InMemory;
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

        HandlerCodegenMode.Apply(builder);

        // Disable Wolverine's SQL durable outbox for this in-memory factory. With it registered,
        // WolverineRuntime.StartAsync → tryMigrateStorage() opens a SqlConnection and migrates the message store
        // against DatabaseSettings:ConnectionString at host start; with no reachable DB that connect times out and
        // the failed startup tears the host down (every test then fails with ObjectDisposedException at
        // CreateClient). The flag makes WolverineConfiguration register NO SQL store (MediatorOnly), so there is
        // nothing to migrate — leaving discovery + codegen + dispatch, all this guard checks. The Testcontainers
        // WaydSqlServerApiFactory leaves this false and provisions the real outbox against its container.
        builder.UseSetting("Wolverine:DisableDurableOutbox", "true");

        // Placeholder settings, as host settings rather than ConfigureAppConfiguration: AddWaydWolverine reads
        // DatabaseSettings:ConnectionString while the host is still being built (and passes it to
        // PersistMessagesWithSqlServer, which parses it), before a deferred ConfigureAppConfiguration override
        // applies. A host setting reaches Program as a command-line argument, which out-ranks the json files and
        // user secrets; without it the read resolves to user secrets locally or database.json's
        // `(localdb)\mssqllocaldb` in CI, and `(localdb)` throws PlatformNotSupportedException at parse time on
        // Linux. The value only has to parse: in-memory EF, in-memory Hangfire and DisableDurableOutbox mean
        // nothing connects to it.
        const string placeholderConnectionString =
            "Server=127.0.0.1,1433;Database=WaydTest;User Id=sa;Password=Placeholder_not_used_1;TrustServerCertificate=true;Connect Timeout=1";
        builder.UseSetting("DatabaseSettings:DBProvider", "mssql");
        builder.UseSetting("DatabaseSettings:ConnectionString", placeholderConnectionString);
        builder.UseSetting("HangfireSettings:Storage:ConnectionString", placeholderConnectionString);
        builder.UseSetting("SecuritySettings:LocalJwt:Secret", "integration-test-secret-key-please-ignore-0123456789");

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
            // server and client still resolve) without any database. The Testcontainers
            // WaydSqlServerApiFactory keeps SQL storage for the real end-to-end pipeline tests.
            services.RemoveAll<JobStorage>();
            services.AddSingleton<JobStorage>(new InMemoryStorage());
        });
    }
}
