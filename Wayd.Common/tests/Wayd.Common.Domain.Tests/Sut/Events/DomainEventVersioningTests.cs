using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Events;

/// <summary>
/// The versioning rules every domain event follows. A breaking change is a new type named with its
/// generation (<c>...EventV2</c>), because consumers dispatch on the type; <see cref="DomainEvent.EventVersion"/>
/// carries only the compatible revisions within that generation.
/// </summary>
/// <remarks>
/// The compiler requires a version, since <see cref="DomainEvent"/> has no default for it, but not that the
/// version agrees with the type's generation. Nor does it notice a superseded type being deleted, which
/// compiles fine while stranding every payload written as it.
/// </remarks>
public sealed partial class DomainEventVersioningTests
{
    private static readonly Type[] EventTypes = typeof(DomainEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(DomainEvent)) && !t.IsAbstract && !t.IsGenericTypeDefinition)
        .ToArray();

    public static TheoryData<string> EventTypeNames() => new(EventTypes.Select(t => t.FullName!));

    [Theory]
    [MemberData(nameof(EventTypeNames))]
    public void EventVersionMajor_MatchesTheTypeGeneration(string typeName)
    {
        // Arrange
        var type = EventTypes.Single(t => t.FullName == typeName);
        var generation = Generation(type);

        // Act
        var version = Construct(type).EventVersion;

        // Assert
        var major = int.Parse(version.Split('.')[0]);
        major.Should().Be(generation,
            $"{type.Name} is generation {generation}, so its EventVersion must be {generation}.x — a breaking " +
            "change is a new type, and a revision within a type never moves the major");
    }

    [Fact]
    public void EverySupersededGeneration_IsKeptAndFrozen()
    {
        // Arrange
        var byName = EventTypes.ToDictionary(t => t.Name);
        var successors = EventTypes.Where(t => Generation(t) > 1);

        foreach (var successor in successors)
        {
            // Act
            var generation = Generation(successor);
            var stem = GenerationSuffix().Replace(successor.Name, string.Empty);
            var predecessorName = generation == 2 ? stem : $"{stem}V{generation - 1}";

            // Assert — payloads written as the predecessor still have to deserialize into it
            byName.Should().ContainKey(predecessorName,
                $"{successor.Name} supersedes {predecessorName}, which must never be deleted");
            byName[predecessorName].GetCustomAttribute<ObsoleteAttribute>().Should().NotBeNull(
                $"{predecessorName} is superseded by {successor.Name} and must be marked [Obsolete] so nothing raises it");
        }
    }

    private static int Generation(Type type)
    {
        var match = GenerationSuffix().Match(type.Name);
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }

    /// <summary>
    /// Builds an instance only to read the version its constructor passes to <see cref="DomainEvent"/>;
    /// the argument values are irrelevant, they only have to get past the constructor body.
    /// </summary>
    private static DomainEvent Construct(Type type)
    {
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetCustomAttribute<JsonConstructorAttribute>() is not null)
            .ThenByDescending(c => c.GetParameters().Length)
            .First();

        var args = ctor.GetParameters().Select(p => Placeholder(p.ParameterType)).ToArray();

        return (DomainEvent)ctor.Invoke(args);
    }

    private static object? Placeholder(Type type)
    {
        if (type == typeof(EventActor)) return EventActor.System;
        if (type == typeof(string)) return string.Empty;
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) return Activator.CreateInstance(type);
        if (type.IsValueType) return Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;
        return null;
    }

    [GeneratedRegex(@"V(\d+)$")]
    private static partial Regex GenerationSuffix();
}
