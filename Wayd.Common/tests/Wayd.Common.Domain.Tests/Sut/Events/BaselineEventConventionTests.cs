using System.Reflection;
using System.Text.Json.Serialization;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Events;

/// <summary>
/// The rules that make a baseline interchangeable with its aggregate's creation event as the start of a
/// history, and keep it out of every consumer's reach. See "Baselining a record that predates its events" in
/// docs/contributing/domain-events.mdx.
/// </summary>
public sealed class BaselineEventConventionTests
{
    public static TheoryData<string> BaselineTypeNames() =>
        new(DomainEventCatalog.EventTypes.Where(IsBaseline).Select(t => t.FullName!));

    [Fact]
    public void AtLeastOneBaselineTypeExists()
    {
        // Arrange / Act
        var baselines = DomainEventCatalog.EventTypes.Where(IsBaseline);

        // Assert — an empty theory passes vacuously, so the rules below would check nothing
        baselines.Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(DomainEventTypeNames))]
    public void BaselineCategory_IsDeclaredOnlyByABaselineType(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);

        // Act
        var category = DomainEventCatalog.Construct(type).GetActivityCategory();

        // Assert
        (category == ActivityCategory.Baseline).Should().Be(IsBaseline(type),
            $"{type.Name} must derive from BaselineEvent<TSelf, TCreated> exactly when it records a baseline, " +
            "so every baseline gets a deterministic id and is never published");
    }

    [Theory]
    [MemberData(nameof(BaselineTypeNames))]
    public void Payload_IsTheCreatedEventsPayload(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        var created = CreatedEventOf(type);

        // Act
        var baselineFields = PayloadFields(type, declaredOnBaselineBase: false);
        var createdFields = PayloadFields(created, declaredOnBaselineBase: false);

        // Assert — the fields every baseline adds are declared once, on the base
        baselineFields.Should().BeEquivalentTo(createdFields,
            $"{type.Name} must carry exactly {created.Name}'s payload, so a history folds the same way from either");
    }

    [Theory]
    [MemberData(nameof(BaselineTypeNames))]
    public void Constructor_TakesTheCreatedEventsParametersWithoutTheActor(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        var created = CreatedEventOf(type);
        var expected = DomainEventCatalog.DeserializationConstructor(created)!.GetParameters()
            .Where(p => p.ParameterType != typeof(EventActor))
            .Select(Describe)
            .Concat([$"recordcreatedon: {typeof(Instant?)}", $"recordcreatedbyid: {typeof(Guid?)}"]);

        // Act
        var ctor = DomainEventCatalog.DeserializationConstructor(type);

        // Assert
        ctor.Should().NotBeNull($"{type.Name} needs one constructor the durable serializer can bind");
        ctor!.GetParameters().Select(Describe).Should().BeEquivalentTo(expected,
            $"{type.Name} is built from exactly what {created.Name} is, less the actor a baseline fixes as the " +
            "system, plus when and by whom the record was created");
    }

    [Theory]
    [MemberData(nameof(BaselineTypeNames))]
    public void Version_MatchesTheCreatedEvent(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        var created = CreatedEventOf(type);

        // Act
        var baselineVersion = DomainEventCatalog.Construct(type).EventVersion;
        var createdVersion = DomainEventCatalog.Construct(created).EventVersion;

        // Assert
        baselineVersion.Should().Be(createdVersion,
            $"{type.Name} is versioned alongside {created.Name}: a field added to one is added to the other");
        DomainEventCatalog.Generation(type).Should().Be(DomainEventCatalog.Generation(created),
            $"a new generation of {created.Name} needs a new generation of {type.Name}");
    }

    [Theory]
    [MemberData(nameof(BaselineTypeNames))]
    public void AggregateType_MatchesTheCreatedEvent(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        var created = CreatedEventOf(type);

        // Act
        var baselineAggregate = ((IAggregateEvent)DomainEventCatalog.Construct(type)).AggregateType;
        var createdAggregate = (DomainEventCatalog.Construct(created) as IAggregateEvent)?.AggregateType;

        // Assert
        baselineAggregate.Should().Be(createdAggregate,
            $"{type.Name} is read by the record's Activity section, which reads by {created.Name}'s aggregate type");
    }

    [Theory]
    [MemberData(nameof(BaselineTypeNames))]
    public void Type_ImplementsNothingAConsumerCouldDispatchOn(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        var allowed = type.BaseType!.GetInterfaces().ToHashSet();

        // Act
        var added = type.GetInterfaces()
            .Where(i => !allowed.Contains(i))
            .Where(i => !(i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>)))
            .Select(i => i.Name)
            .ToArray();

        // Assert
        added.Should().BeEmpty(
            $"{type.Name} carries the whole record and is never delivered — a module marker such as IPpmEvent " +
            "would hand it to every projection that handles the marker");
    }

    public static TheoryData<string> DomainEventTypeNames() => DomainEventCatalog.EventTypeNames();

    private static bool IsBaseline(Type type) => BaselineBase(type) is not null;

    private static Type? BaselineBase(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BaselineEvent<,>))
                return current;
        }

        return null;
    }

    private static Type CreatedEventOf(Type baselineType) => BaselineBase(baselineType)!.GetGenericArguments()[1];

    private static string[] PayloadFields(Type type, bool declaredOnBaselineBase) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Where(p => IsDeclaredOnBaselineBase(p) == declaredOnBaselineBase)
            .Select(p => $"{p.Name}: {p.PropertyType}")
            .ToArray();

    private static string Describe(ParameterInfo parameter) =>
        $"{parameter.Name!.ToLowerInvariant()}: {parameter.ParameterType}";

    private static bool IsDeclaredOnBaselineBase(PropertyInfo property) =>
        property.DeclaringType is { IsGenericType: true } declaring
        && declaring.GetGenericTypeDefinition() == typeof(BaselineEvent<,>);
}
