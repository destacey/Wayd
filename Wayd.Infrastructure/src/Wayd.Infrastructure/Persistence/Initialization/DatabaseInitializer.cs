using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Persistence.Initialization;

internal class DatabaseInitializer(WaydDbContext context, IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    /// <summary>
    /// How long a single migration command may run. Data migrations that reconstruct history from the
    /// audit trail read every row of a table that grows without bound, which comfortably outlives the
    /// 30-second ADO.NET default. Applied only while migrations run, then restored, so no runtime query
    /// inherits it — a slow query at runtime should still fail fast.
    /// </summary>
    private const int MigrationCommandTimeoutSeconds = 180;

    private readonly WaydDbContext _context = context;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger = logger;

    public async Task InitializeDatabase(CancellationToken cancellationToken)
    {
        await InitializeDb(cancellationToken);
        await InitializeApplicationDb(cancellationToken);
    }

    public async Task InitializeApplicationDb(CancellationToken cancellationToken)
    {
        // First create a new scope
        using var scope = _serviceProvider.CreateScope();

        // Then run the initialization in the new scope
        await scope.ServiceProvider.GetRequiredService<ApplicationDbInitializer>()
            .Initialize(cancellationToken);
    }

    private async Task InitializeDb(CancellationToken cancellationToken)
    {
        if (_context.Database.GetPendingMigrations().Any())
        {
            _logger.LogInformation("Applying Root Migrations.");

            var originalTimeout = _context.Database.GetCommandTimeout();
            _context.Database.SetCommandTimeout(MigrationCommandTimeoutSeconds);

            try
            {
                await _context.Database.MigrateAsync(cancellationToken);
            }
            finally
            {
                _context.Database.SetCommandTimeout(originalTimeout);
            }
        }
    }
}