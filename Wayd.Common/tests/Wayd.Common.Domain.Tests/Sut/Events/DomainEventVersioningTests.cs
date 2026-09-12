using System.Reflection;
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
public sealed class DomainEventVersioningTests
{
    public static TheoryData<string> EventTypeNames() => DomainEventCatalog.EventTypeNames();

    [Theory]
    [MemberData(nameof(EventTypeNames))]
    public void EventVersionMajor_MatchesTheTypeGeneration(string typeName)
    {
        // Arrange
        var type = DomainEventCatalog.ByName(typeName);
        var generation = DomainEventCatalog.Generation(type);

        // Act
        var version = DomainEventCatalog.Construct(type).EventVersion;

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
        var byName = DomainEventCatalog.EventTypes.ToDictionary(t => t.Name);
        var successors = DomainEventCatalog.EventTypes.Where(t => DomainEventCatalog.Generation(t) > 1);

        foreach (var successor in successors)
        {
            // Act
            var generation = DomainEventCatalog.Generation(successor);
            var stem = DomainEventCatalog.Stem(successor);
            var predecessorName = generation == 2 ? stem : $"{stem}V{generation - 1}";

            // Assert — payloads written as the predecessor still have to deserialize into it
            byName.Should().ContainKey(predecessorName,
                $"{successor.Name} supersedes {predecessorName}, which must never be deleted");
            byName[predecessorName].GetCustomAttribute<ObsoleteAttribute>().Should().NotBeNull(
                $"{predecessorName} is superseded by {successor.Name} and must be marked [Obsolete] so nothing raises it");
        }
    }

}
