using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wayd.Infrastructure.Auditing;

/// <summary>
/// How a property's value is written into an audit trail payload. A trail row must never hold a value in a
/// weaker form than the column it describes, so a property that is protected in its own column, or kept out of
/// events and logs, says so here and the audit loop follows it.
/// </summary>
public enum AuditTrailValue
{
    /// <summary>The value as the application sees it. The default for every property without the annotation.</summary>
    Current = 0,

    /// <summary>
    /// The value as the column stores it, after the property's value converter has run. Set by
    /// <c>HasEncryptedJsonConversion</c>, so a connection's secrets reach the trail encrypted, exactly as they are
    /// in their own column.
    /// </summary>
    Stored = 1,

    /// <summary>
    /// No value at all. A change still names the property in the row's affected columns, so the trail shows that
    /// it changed, but neither its old nor its new value is written.
    /// </summary>
    Excluded = 2,
}

/// <summary>
/// Reads and writes the <see cref="AuditTrailValue"/> annotation on a property, and produces the value the audit
/// trail records for it.
/// </summary>
public static class AuditTrailValues
{
    /// <summary>
    /// The model annotation that carries a property's <see cref="AuditTrailValue"/>, as its name. A string rather
    /// than the enum, because the annotation is written into the migrations model snapshot.
    /// </summary>
    public const string AnnotationName = "Wayd:AuditTrailValue";

    /// <summary>The annotation <see cref="For"/> reads back as <paramref name="value"/>.</summary>
    public static string AnnotationValue(AuditTrailValue value) => value.ToString();

    /// <summary>How the trail records <paramref name="property"/>; <see cref="AuditTrailValue.Current"/> unless annotated.</summary>
    public static AuditTrailValue For(IReadOnlyProperty property) =>
        property.FindAnnotation(AnnotationName)?.Value is string name
            && Enum.TryParse<AuditTrailValue>(name, out var value)
                ? value
                : AuditTrailValue.Current;

    /// <summary>
    /// The value to write for <paramref name="entry"/>, or <see langword="false"/> when the property is excluded
    /// and nothing may be written.
    /// </summary>
    /// <param name="entry">The tracked property.</param>
    /// <param name="current">The current value when true, the original value when false.</param>
    /// <param name="value">The value to record.</param>
    public static bool TryGet(PropertyEntry entry, bool current, out object? value)
    {
        var raw = current ? entry.CurrentValue : entry.OriginalValue;
        switch (For(entry.Metadata))
        {
            case AuditTrailValue.Excluded:
                value = null;
                return false;

            case AuditTrailValue.Stored:
                value = ToStored(entry.Metadata, raw);
                return true;

            default:
                value = raw;
                return true;
        }
    }

    // The stored form of a JSON column is JSON text. Recording it as a parsed element nests it in the payload
    // the way the unconverted object was, instead of as one escaped string.
    private static object? ToStored(IReadOnlyProperty property, object? raw)
    {
        if (raw is null)
            return null;

        var converter = property.GetValueConverter()
            ?? throw new InvalidOperationException(
                $"{property.DeclaringType.DisplayName()}.{property.Name} is recorded in its stored form but has no value converter.");

        var stored = converter.ConvertToProvider(raw);
        if (stored is not string text)
            return stored;

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return text;
        }
    }
}
