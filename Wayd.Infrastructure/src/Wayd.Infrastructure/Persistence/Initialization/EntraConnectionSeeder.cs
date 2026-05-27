using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Infrastructure.Auth.AzureAd;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// One-time bridge: seeds an <see cref="EntraConnection"/> row from <c>SecuritySettings:AzureAd:*</c>
/// the first time the app boots after employee sync was moved off of configuration and onto the
/// connector framework. After the row exists, admins manage it via the Settings UI.
/// </summary>
/// <remarks>
/// Insert-only: never updates an existing row. Once seeded, the database is the source of truth —
/// re-syncing from config on every startup would silently undo admin edits made through the UI.
/// </remarks>
public class EntraConnectionSeeder(
    IOptions<AzureAdSettings> azureAdSettings,
    ILogger<EntraConnectionSeeder> logger) : ICustomSeeder
{
    private readonly AzureAdSettings _azureAdSettings = azureAdSettings.Value;
    private readonly ILogger<EntraConnectionSeeder> _logger = logger;

    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        // If any EntraConnection already exists, the migration is done — nothing to seed.
        var existing = await dbContext.EntraConnections.AnyAsync(cancellationToken);
        if (existing) return;

        if (string.IsNullOrWhiteSpace(_azureAdSettings.TenantId)
            || string.IsNullOrWhiteSpace(_azureAdSettings.ClientId)
            || string.IsNullOrWhiteSpace(_azureAdSettings.ClientSecret))
        {
            _logger.LogDebug("EntraConnectionSeeder: SecuritySettings:AzureAd is incomplete; nothing to seed.");
            return;
        }

        var timestamp = dateTimeProvider.Now;
        var configuration = new EntraConnectionConfiguration(
            tenantId: _azureAdSettings.TenantId!,
            clientId: _azureAdSettings.ClientId!,
            clientSecret: _azureAdSettings.ClientSecret!);

        var connection = EntraConnection.Create(
            name: "Entra (seeded from config)",
            description: "Seeded from SecuritySettings:AzureAd on first boot after employee sync was migrated to the connector framework. Manage via Settings → Connections.",
            configuration: configuration,
            configurationIsValid: true,
            timestamp: timestamp);

        dbContext.EntraConnections.Add(connection);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded EntraConnection from SecuritySettings:AzureAd. People sync now reads from the connector framework; manage the connection via the Settings UI.");
    }
}
