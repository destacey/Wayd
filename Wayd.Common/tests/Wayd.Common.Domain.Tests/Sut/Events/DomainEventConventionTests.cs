using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Events;

/// <summary>
/// The parts of "Designing an event" (docs/contributing/domain-events.mdx) that a type can be checked for
/// without running the aggregate that raises it.
/// </summary>
public sealed partial class DomainEventConventionTests
{
    /// <summary>
    /// Whole-record events raised under the aggregate's bare name, which predate the convention. Each is to be
    /// superseded (#792); once it is <c>[Obsolete]</c> the rule no longer applies to it and its entry here goes.
    /// </summary>
    private static readonly HashSet<string> LegacyWholeRecordEvents =
    [
        "IterationUpdatedEvent",
        "StrategicThemeUpdatedEvent",
        "TeamUpdatedEvent",
        "WorkIterationUpdatedEvent",
    ];

    public static TheoryData<string> EventTypeNames() => DomainEventCatalog.EventTypeNames();

    [Theory]
    [MemberData(nameof(EventTypeNames))]
    public void DeserializationConstructor_BindsEveryParameterToAProperty(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .ToArray();

        // Act
        var ctor = DomainEventCatalog.DeserializationConstructor(type);

        // Assert
        ctor.Should().NotBeNull(
            $"{type.Name} has more than one public constructor and none is marked [JsonConstructor], so the " +
            "durable outbox cannot deserialize it");

        foreach (var parameter in ctor!.GetParameters())
        {
            var property = properties.SingleOrDefault(p =>
                string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

            property.Should().NotBeNull(
                $"{type.Name}'s deserialization constructor takes '{parameter.Name}', which binds to no property " +
                "of that name — pass the event's own fields to a [JsonConstructor], not a model it reads them from");
            property!.PropertyType.Should().Be(parameter.ParameterType,
                $"{type.Name}.{property.Name} must have exactly the type of the constructor parameter it binds to");
        }
    }

    [Theory]
    [MemberData(nameof(EventTypeNames))]
    public void Name_SaysWhatChangedRatherThanNamingOnlyTheRecord(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        if (type.GetCustomAttribute<ObsoleteAttribute>() is not null || LegacyWholeRecordEvents.Contains(type.Name))
            return;

        // Act
        var match = GenericChangeName().Match(DomainEventCatalog.Stem(type));
        var aggregateType = (DomainEventCatalog.Construct(type) as IAggregateEvent)?.AggregateType;

        // Assert
        var namesOnlyTheRecord = match.Success && match.Groups["subject"].Value == aggregateType;
        namesOnlyTheRecord.Should().BeFalse(
            $"{type.Name} says only that something about the {aggregateType} is different. Name what happened: " +
            "a DetailsUpdated event for descriptive fields, or the transition itself");
    }

    [Fact]
    public void LegacyWholeRecordEvents_AreStillLiveTypes()
    {
        // Arrange
        var byName = DomainEventCatalog.EventTypes.ToDictionary(t => t.Name);

        foreach (var name in LegacyWholeRecordEvents)
        {
            // Act
            var type = byName.GetValueOrDefault(name);

            // Assert
            type.Should().NotBeNull($"{name} is allow-listed but no longer exists");
            type!.GetCustomAttribute<ObsoleteAttribute>().Should().BeNull(
                $"{name} has been superseded, so the naming rule no longer applies to it — remove its allow-list entry");
        }
    }

    [Theory]
    [MemberData(nameof(EventTypeNames))]
    public void Payload_IdentifiesPeopleByIdOnly(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);

        // Act
        var personalFields = PayloadProperties(type, type.Name, [])
            .Where(path => PersonalDataName().IsMatch(path.Split('.')[^1]))
            .ToArray();

        // Assert
        personalFields.Should().BeEmpty(
            "the activity log is append-only, so personal data written into a payload can never be corrected " +
            "or erased — carry EmployeeId or UserId and resolve the person when the entry is displayed");
    }

    /// <summary>
    /// Every property reachable from the payload, as a dotted path, descending into the domain's own types —
    /// value objects, nested records, collection elements — but not into framework types.
    /// </summary>
    private static IEnumerable<string> PayloadProperties(Type type, string path, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            yield break;

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                continue;

            var propertyPath = $"{path}.{property.Name}";
            yield return propertyPath;

            foreach (var inner in DomainTypesWithin(property.PropertyType))
            {
                foreach (var nested in PayloadProperties(inner, propertyPath, visited))
                    yield return nested;
            }
        }
    }

    private static IEnumerable<Type> DomainTypesWithin(Type type)
    {
        var candidates = type.IsArray ? [type.GetElementType()!]
            : type.IsGenericType ? type.GetGenericArguments()
            : new[] { type };

        return candidates
            .Select(t => Nullable.GetUnderlyingType(t) ?? t)
            .Where(t => t.Assembly == typeof(DomainEvent).Assembly && !t.IsEnum);
    }

    [GeneratedRegex(@"^(?<subject>[A-Za-z]+?)(Updated|Changed)Event$")]
    private static partial Regex GenericChangeName();

    [GeneratedRegex("Email|Phone|FirstName|LastName|FullName|DisplayName|UserName|EmployeeName")]
    private static partial Regex PersonalDataName();
}
