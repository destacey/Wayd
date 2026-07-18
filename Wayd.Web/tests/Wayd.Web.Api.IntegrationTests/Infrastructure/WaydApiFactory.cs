using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayd.Infrastructure.Persistence.Context;

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

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:DBProvider"] = "mssql",
                ["DatabaseSettings:ConnectionString"] = "Server=(localdb)\\test;Database=WaydTest;Trusted_Connection=True;",
                ["HangfireSettings:Storage:ConnectionString"] = "Server=(localdb)\\test;Database=WaydTest;Trusted_Connection=True;",
                ["SecuritySettings:LocalJwt:Secret"] = "integration-test-secret-key-please-ignore-0123456789",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace the SQL Server WaydDbContext with the in-memory provider so nothing connects.
            // Both AddDbContext calls register their provider into the app service provider, so give the
            // in-memory context its own internal service provider to avoid the "multiple database
            // providers registered" conflict.
            services.RemoveAll(typeof(DbContextOptions<WaydDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll<WaydDbContext>();

            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<WaydDbContext>(options => options
                .UseInMemoryDatabase("wolverine-config-check")
                .UseInternalServiceProvider(inMemoryProvider));
        });
    }
}
