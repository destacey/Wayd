using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Carries a system time zone saved in the scheduling settings, as <c>defaultTimeZone</c>, over to
/// <see cref="GeneralSettings.TimeZone"/>, where it now lives.
/// </summary>
/// <remarks>
/// Saved through the settings store rather than written to the table, so the zone is validated and the copy is
/// in the general settings' Activity like any other change. Runs on every boot and does nothing once the
/// general settings have a row, so a zone an administrator later changes is never overwritten. The scheduling
/// row keeps its old value, which reading that section ignores.
/// </remarks>
internal sealed class SystemTimeZoneSeeder(ISystemSettingsStore store, ILogger<SystemTimeZoneSeeder> logger) : ICustomSeeder
{
    private const string LegacyPropertyName = "defaultTimeZone";

    private readonly ISystemSettingsStore _store = store;
    private readonly ILogger<SystemTimeZoneSeeder> _logger = logger;

    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        var generalId = SystemSettingsSection.IdFor<GeneralSettings>();
        if (await dbContext.SystemSettingsSections.AnyAsync(s => s.Id == generalId, cancellationToken))
            return;

        var schedulingId = SystemSettingsSection.IdFor<SchedulingSettings>();
        var scheduling = await dbContext.SystemSettingsSections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schedulingId, cancellationToken);

        var timeZone = scheduling is null ? null : ReadLegacyTimeZone(scheduling.Value);
        if (string.IsNullOrWhiteSpace(timeZone))
            return;

        // Saving the default creates no row, so a scheduling row holding UTC is read again on the next boot and
        // again changes nothing.
        var result = await _store.Save(new GeneralSettings { TimeZone = timeZone }, EventActor.System, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Could not carry the scheduling time zone {TimeZone} over to the general settings: {Error}. The system time zone is UTC until an administrator sets it.",
                timeZone, result.Error);
            return;
        }

        _logger.LogInformation("Carried the scheduling time zone {TimeZone} over to the general settings.", timeZone);
    }

    private static string? ReadLegacyTimeZone(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(LegacyPropertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
