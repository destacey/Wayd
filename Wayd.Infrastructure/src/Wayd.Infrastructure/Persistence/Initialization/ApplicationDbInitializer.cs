using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Persistence.Initialization;

internal class ApplicationDbInitializer
{
    private readonly WaydDbContext _dbContext;
    private readonly ApplicationDbSeeder _dbSeeder;
    private readonly ConnectionSecretBackfill _secretBackfill;
    private readonly ILogger<ApplicationDbInitializer> _logger;

    public ApplicationDbInitializer(
        WaydDbContext dbContext,
        ApplicationDbSeeder dbSeeder,
        ConnectionSecretBackfill secretBackfill,
        ILogger<ApplicationDbInitializer> logger)
    {
        _dbContext = dbContext;
        _dbSeeder = dbSeeder;
        _secretBackfill = secretBackfill;
        _logger = logger;
    }

    public async Task Initialize(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.GetMigrations().Any())
        {
            if ((await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                _logger.LogInformation("Applying Migrations");
                await _dbContext.Database.MigrateAsync(cancellationToken);
            }

            if (await _dbContext.Database.CanConnectAsync(cancellationToken))
            {
                _logger.LogInformation("Connection to Database Succeeded.");

                await _dbSeeder.SeedDatabase(_dbContext, cancellationToken);
                await _secretBackfill.Run(cancellationToken);
            }
        }
    }
}
