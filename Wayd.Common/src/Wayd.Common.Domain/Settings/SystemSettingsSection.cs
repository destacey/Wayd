using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events.Settings;

namespace Wayd.Common.Domain.Settings;

/// <summary>
/// The stored values of one <see cref="ISettingsSection{TSelf}"/>, as a JSON document.
/// </summary>
/// <remarks>
/// The id is derived from the scope and key rather than generated, so a section has exactly one possible row
/// per scope and its Activity section can be read without looking the row up — including before one exists.
/// </remarks>
public sealed class SystemSettingsSection : BaseAuditableEntity<Guid>
{
    // Changing this gives every section a new id, orphaning its row and its activity history.
    private static readonly Guid _idNamespace = new("0d6f3b8e-2c41-4a7e-b5d9-8e1f7a3c6b24");

    // Without the NodaTime converters a LocalTime or Period would serialize as an object of its fields and
    // then fail to deserialize, so a section using one could save but never be read back.
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    private SystemSettingsSection() { }

    private SystemSettingsSection(SettingsScope scope, string key)
    {
        Id = IdFor(scope, key);
        Scope = scope;
        Key = key;
        Value = "{}";
    }

    public string Key { get; private init; } = default!;

    public SettingsScope Scope { get; private init; }

    /// <summary>
    /// The section's values as JSON. Holds only what has been saved; anything absent reads as the code default.
    /// </summary>
    public string Value { get; private set; } = default!;

    /// <summary>The <see cref="ISettingsSection{TSelf}.SchemaVersion"/> <see cref="Value"/> was written at.</summary>
    public int SchemaVersion { get; private set; }

    public static Guid IdFor(SettingsScope scope, string key) =>
        NameBasedUuid.Create(_idNamespace, $"{scope}:{key}");

    public static Guid IdFor<TSection>(SettingsScope scope = SettingsScope.System)
        where TSection : class, ISettingsSection<TSection>, new() =>
        IdFor(scope, TSection.Key);

    /// <summary>
    /// The values <paramref name="stored"/> holds, with the code defaults for anything it does not — all of
    /// them when nothing has been stored.
    /// </summary>
    public static TSection Read<TSection>(SystemSettingsSection? stored)
        where TSection : class, ISettingsSection<TSection>, new()
    {
        if (stored is null)
            return new TSection();

        if (stored.Key != TSection.Key)
            throw new InvalidOperationException($"Section '{stored.Key}' cannot be read as '{TSection.Key}'.");

        return JsonSerializer.Deserialize<TSection>(stored.Value, _jsonOptions) ?? new TSection();
    }

    /// <summary>
    /// Starts storing a section that has so far read as its defaults. Returns null when
    /// <paramref name="values"/> are the defaults, so a save that changes nothing does not pin them: a section
    /// with no row keeps following the code defaults as they change.
    /// </summary>
    public static SystemSettingsSection? Create<TSection>(TSection values, EventActor actor, Instant timestamp)
        where TSection : class, ISettingsSection<TSection>, new()
    {
        var section = new SystemSettingsSection(SettingsScope.System, TSection.Key);
        return section.Change(values, actor, timestamp) ? section : null;
    }

    /// <summary>
    /// Replaces the section's values, raising <see cref="SystemSettingsSectionValuesChangedEvent"/> when they
    /// differ from the values in effect. Returns whether anything changed.
    /// </summary>
    public bool Change<TSection>(TSection values, EventActor actor, Instant timestamp)
        where TSection : class, ISettingsSection<TSection>, new()
    {
        ArgumentNullException.ThrowIfNull(values);

        var before = Read<TSection>(this);

        // A round trip compares what would be read back, not what the caller passed.
        var value = JsonSerializer.Serialize(values, _jsonOptions);
        var after = JsonSerializer.Deserialize<TSection>(value, _jsonOptions) ?? new TSection();

        if (EqualityComparer<TSection>.Default.Equals(before, after))
            return false;

        Value = value;
        SchemaVersion = TSection.SchemaVersion;

        AddDomainEvent(new SystemSettingsSectionValuesChangedEvent(
            Id,
            Key,
            Scope,
            SchemaVersion,
            JsonSerializer.SerializeToElement(before, _jsonOptions),
            JsonSerializer.SerializeToElement(after, _jsonOptions),
            actor,
            timestamp));

        return true;
    }
}
