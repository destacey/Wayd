using System.Text.Json;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.Common.Domain.Tests.Sut.Events;

public sealed class ProjectBaselinedEventTests
{
    private static readonly JsonSerializerOptions Options =
        new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    [Fact]
    public void WrittenTwiceForOneProject_HasOneEventId()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();

        // Act
        var first = Baseline(projectId, Instant.FromUtc(2026, 9, 1, 8, 0));
        var second = Baseline(projectId, Instant.FromUtc(2026, 9, 13, 8, 0));

        // Assert
        first.EventId.Should().Be(second.EventId);
        first.EventId.Should().Be(BaselineEventId.For("Project", projectId));
    }

    [Fact]
    public void IsAttributedToTheSystemAndBadgedAsABaseline()
    {
        // Arrange / Act
        var baseline = Baseline(Guid.CreateVersion7(), Instant.FromUtc(2026, 9, 13, 8, 0));

        // Assert
        baseline.Actor.Should().Be(EventActor.System);
        baseline.GetActivityCategory().Should().Be(ActivityCategory.Baseline);
        baseline.AggregateType.Should().Be("Project");
    }

    [Fact]
    public void RoundTrips_KeepingItsDerivedEventId()
    {
        // Arrange — EventId and Actor are not constructor parameters, so they come back through their setters
        var original = Baseline(Guid.CreateVersion7(), Instant.FromUtc(2026, 9, 13, 8, 0));

        // Act
        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<ProjectBaselinedEvent>(json, Options)!;

        // Assert
        roundTripped.EventId.Should().Be(original.EventId);
        roundTripped.Actor.Should().Be(original.Actor);
        roundTripped.Timestamp.Should().Be(original.Timestamp);
        roundTripped.Key.Should().Be(original.Key);
        roundTripped.RecordCreatedOn.Should().Be(original.RecordCreatedOn);
        roundTripped.RecordCreatedById.Should().Be(original.RecordCreatedById);
        json.Should().NotContain("AggregateType");
    }

    private static ProjectBaselinedEvent Baseline(Guid projectId, Instant timestamp) =>
        new(projectId, new ProjectKey("APOLLO"), "Apollo", "A project.", 1, 1, null, Guid.CreateVersion7(), null,
            null, null, [], [], Instant.FromUtc(2024, 3, 4, 15, 30), Guid.CreateVersion7(), timestamp);
}
