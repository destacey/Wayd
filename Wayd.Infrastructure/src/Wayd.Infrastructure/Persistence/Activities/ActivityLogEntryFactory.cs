using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Events;

namespace Wayd.Infrastructure.Persistence.Activities;

/// <summary>
/// Builds the <see cref="ActivityLogEntry"/> for a raised domain event.
/// </summary>
/// <remarks>
/// Separate from <c>BaseDbContext</c>, which drains events and is the only caller on the live path, so
/// that a backfill replaying historical facts into the log produces entries indistinguishable from the
/// ones a live event writes. A second serializer configuration or a second summary format would surface
/// as payloads that will not compare and summaries that read differently for the same event type.
/// </remarks>
internal static partial class ActivityLogEntryFactory
{
    internal static readonly JsonSerializerOptions ActivityJsonOptions = CreateActivityJsonOptions();

    private static JsonSerializerOptions CreateActivityJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        return options;
    }

    internal static ActivityLogEntry CreateActivityLogEntry(DomainEvent domainEvent, IEntity entity, int ordinal, string? correlationId)
    {
        var eventType = domainEvent.GetType().Name;

        string aggregateType;
        Guid aggregateId;

        if (domainEvent is IAggregateEvent aggEvent)
        {
            aggregateType = aggEvent.AggregateType;
            aggregateId = aggEvent.AggregateId;
        }
        else
        {
            aggregateType = entity.GetType().Name;
            aggregateId = ResolveAggregateId(entity, domainEvent);
        }

        var entityNamespace = entity.GetType().Namespace ?? string.Empty;
        var eventNamespace = domainEvent.GetType().Namespace ?? string.Empty;
        var domainArea = ResolveDomainArea(entityNamespace, eventNamespace);

        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), ActivityJsonOptions);
        var summary = FormatSummary(eventType, aggregateType);

        return new ActivityLogEntry(
            domainEvent.EventId,
            eventType,
            domainArea,
            aggregateType,
            aggregateId,
            domainEvent.Actor,
            domainEvent.Timestamp,
            ordinal,
            correlationId,
            payload,
            summary,
            domainEvent.EventVersion);
    }

    private static string ResolveDomainArea(string entityNamespace, string eventNamespace)
    {
        if (entityNamespace.Contains("ProjectPortfolioManagement") || eventNamespace.Contains("ProjectPortfolioManagement"))
            return "Ppm";
        if (entityNamespace.Contains("Organization") || eventNamespace.Contains("Organization"))
            return "Organization";
        if (entityNamespace.Contains("ProductManagement") || eventNamespace.Contains("ProductManagement"))
            return "ProductManagement";
        if (entityNamespace.Contains("Planning") || eventNamespace.Contains("Planning"))
            return "Planning";
        if (entityNamespace.Contains("Work") || eventNamespace.Contains("Work"))
            return "Work";
        if (entityNamespace.Contains("StrategicManagement") || eventNamespace.Contains("StrategicManagement"))
            return "StrategicManagement";
        if (entityNamespace.Contains("StatusWorkflow") || eventNamespace.Contains("StatusWorkflow"))
            return "StatusWorkflows";
        if (entityNamespace.Contains("Identity") || eventNamespace.Contains("Identity"))
            return "Identity";
        if (entityNamespace.Contains("Links") || eventNamespace.Contains("Links"))
            return "Links";
        if (entityNamespace.Contains("AppIntegration") || eventNamespace.Contains("AppIntegration"))
            return "AppIntegration";

        return "App";
    }

    private static Guid ResolveAggregateId(IEntity entity, DomainEvent domainEvent)
    {
        if (entity is IEntity<Guid> guidEntity && guidEntity.Id != Guid.Empty)
        {
            return guidEntity.Id;
        }

        var idProp = entity.GetType().GetProperty("Id");
        if (idProp?.GetValue(entity) is Guid gid && gid != Guid.Empty)
        {
            return gid;
        }

        if (idProp?.GetValue(entity) is string sid && Guid.TryParse(sid, out var parsedGuid) && parsedGuid != Guid.Empty)
        {
            return parsedGuid;
        }

        var eventIdProp = domainEvent.GetType().GetProperty("Id");
        if (eventIdProp?.GetValue(domainEvent) is Guid eventGuid && eventGuid != Guid.Empty)
        {
            return eventGuid;
        }

        return domainEvent.EventId;
    }

    private static string FormatSummary(string eventType, string aggregateType)
    {
        // The generation suffix of a superseding type ("ProjectReparentedEventV2") marks a contract break
        // for consumers; a reader of the log is looking at the same fact either way.
        var unversioned = TypeGenerationSuffix().Replace(eventType, string.Empty);

        var readableEvent = unversioned.EndsWith("Event", StringComparison.Ordinal)
            ? unversioned[..^5]
            : unversioned;

        // Matched before the words are spaced out, so a multi-word aggregate still recognizes its own
        // events: "ProjectPortfolioCreated" starts with "ProjectPortfolio", while "Project Portfolio
        // Created" does not, and appending "on ProjectPortfolio" to it would say the same thing twice.
        if (readableEvent.StartsWith(aggregateType, StringComparison.OrdinalIgnoreCase))
        {
            return SpaceWords(readableEvent);
        }

        return $"{SpaceWords(readableEvent)} on {SpaceWords(aggregateType)}";
    }

    private static string SpaceWords(string pascalCase) => Regex.Replace(pascalCase, "(\\B[A-Z])", " $1");

    [GeneratedRegex(@"V\d+$")]
    private static partial Regex TypeGenerationSuffix();

    /// <summary>
    /// Builds an entry for an event that names its own aggregate, with no entity in hand.
    /// </summary>
    /// <remarks>
    /// The entity-based overload consults the entity only to fill in an aggregate the event did not
    /// declare and to help resolve the domain area. An <see cref="IAggregateEvent"/> answers both from
    /// its own type, which is what lets a backfill construct an entry from a database row.
    /// </remarks>
    internal static ActivityLogEntry CreateActivityLogEntry(DomainEvent domainEvent, IAggregateEvent aggregateEvent, int ordinal, string? correlationId)
    {
        var eventType = domainEvent.GetType().Name;
        var domainArea = ResolveDomainArea(string.Empty, domainEvent.GetType().Namespace ?? string.Empty);

        return new ActivityLogEntry(
            domainEvent.EventId,
            eventType,
            domainArea,
            aggregateEvent.AggregateType,
            aggregateEvent.AggregateId,
            domainEvent.Actor,
            domainEvent.Timestamp,
            ordinal,
            correlationId,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), ActivityJsonOptions),
            FormatSummary(eventType, aggregateEvent.AggregateType),
            domainEvent.EventVersion);
    }
}
