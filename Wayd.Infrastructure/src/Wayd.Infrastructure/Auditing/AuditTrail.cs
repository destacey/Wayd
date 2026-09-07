using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Wayd.Infrastructure.Auditing;

public class AuditTrail
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuditTrail(EntityEntry entry, IDateTimeProvider dateTimeProvider)
    {
        Entry = entry;
        _dateTimeProvider = dateTimeProvider;
    }

    public EntityEntry Entry { get; }
    public required string UserId { get; set; }
    public string? SchemaName { get; set; }
    public string? TableName { get; set; }
    public Dictionary<string, object?> KeyValues { get; } = [];
    public Dictionary<string, object?> OldValues { get; } = [];
    public Dictionary<string, object?> NewValues { get; } = [];
    public List<PropertyEntry> TemporaryProperties { get; } = [];
    public TrailType TrailType { get; set; }
    public List<string> ChangedColumns { get; } = [];
    public bool HasTemporaryProperties => TemporaryProperties.Count > 0;
    public string? CorrelationId { get; set; }

    public Trail ToAuditTrail() =>
        new()
        {
            UserId = UserId,
            Type = TrailType.ToString(),
            SchemaName = SchemaName,
            TableName = TableName,
            DateTime = _dateTimeProvider.Now,
            PrimaryKey = SerializeForAudit(KeyValues),
            OldValues = OldValues.Count == 0 ? null : SerializeForAudit(OldValues),
            NewValues = NewValues.Count == 0 ? null : SerializeForAudit(NewValues),
            AffectedColumns = ChangedColumns.Count == 0 ? null : SerializeForAudit(ChangedColumns),
            CorrelationId = CorrelationId
        };

    private static string SerializeForAudit(object obj)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        return JsonSerializer.Serialize(obj, options);
    }
}