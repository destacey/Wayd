using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Carries a system time zone saved in the scheduling settings, as <c>defaultTimeZone</c>, over to
/// <see cref="GeneralSettings.TimeZone"/>, where it now lives.
/// </summary>
/// <remarks>
/// <para>
/// Runs on every boot and does nothing once the general settings have a row, so a zone an administrator later
/// changes is never overwritten. The scheduling row keeps its old value, which reading that section ignores.
/// </para>
/// <para>
/// The zone is validated as a save through the settings store would be, and the change is recorded in the
/// general settings' Activity. The event is taken off the row before saving, because Wolverine cannot route
/// while seeders run, before the host starts: SaveChanges would otherwise publish it into a runtime that drops
/// it, or fails the boot once the event is routed durably.
/// </para>
/// </remarks>
internal sealed class SystemTimeZoneSeeder(
    IEnumerable<IValidator<GeneralSettings>> validators,
    ILogger<SystemTimeZoneSeeder> logger) : ICustomSeeder
{
    private const string LegacyPropertyName = "defaultTimeZone";

    private readonly IEnumerable<IValidator<GeneralSettings>> _validators = validators;
    private readonly ILogger<SystemTimeZoneSeeder> _logger = logger;

    /// <summary>
    /// Copies the legacy scheduling time zone into the general settings when they have no row yet. A missing,
    /// unreadable or invalid zone leaves the system time zone at its default, UTC.
    /// </summary>
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

        var values = new GeneralSettings { TimeZone = timeZone.Trim() };
        foreach (var validator in _validators)
        {
            var validation = await validator.ValidateAsync(values, cancellationToken);
            if (!validation.IsValid)
            {
                // Repeats each boot until the scheduling row is saved again, which drops the old value.
                _logger.LogWarning(
                    "The scheduling settings hold the time zone {TimeZone}, which is not valid, so it was not carried over to the general settings. The system time zone stays UTC until an administrator sets it.",
                    timeZone);
                return;
            }
        }

        // Null when the zone is the default: nothing to record, and nothing to store.
        var section = SystemSettingsSection.Create(values, EventActor.System, dateTimeProvider.Now);
        if (section is null)
            return;

        var ordinal = 0;
        foreach (var domainEvent in section.DomainEvents)
            dbContext.ActivityLogs.Add(ActivityLogEntryFactory.CreateActivityLogEntry(domainEvent, section, ordinal++, correlationId: null));

        section.ClearDomainEvents();
        dbContext.SystemSettingsSections.Add(section);

        try
        {
            // One save, so the row is never committed without the entry recording it.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The id is derived from the key: another instance booting at the same time carried the zone over.
            dbContext.ChangeTracker.Clear();
            _logger.LogInformation("Another instance carried the scheduling time zone over to the general settings first.");
            return;
        }

        _logger.LogInformation("Carried the scheduling time zone {TimeZone} over to the general settings.", values.TimeZone);
    }

    private string? ReadLegacyTimeZone(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(LegacyPropertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException ex)
        {
            // Reading the scheduling settings fails on the same row; that is where it surfaces, not at boot.
            _logger.LogWarning(ex, "The scheduling settings row is not valid JSON, so no time zone was carried over from it.");
            return null;
        }
    }
}
