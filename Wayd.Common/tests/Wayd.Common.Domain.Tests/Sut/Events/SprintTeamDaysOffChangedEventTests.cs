using System.Text.Json;
using System.Text.Json.Nodes;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Domain.Tests.Sut.Events;

public sealed class SprintTeamDaysOffChangedEventTests
{
    private static readonly JsonSerializerOptions Options =
        new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    private static readonly LocalDate Friday = new(2026, 10, 2);
    private static readonly LocalDate Monday = new(2026, 10, 5);

    [Fact]
    public void RoundTrips_KeepingThePartDays()
    {
        // Arrange
        var original = new SprintTeamDaysOffChangedEvent(Guid.CreateVersion7(), 9, [Monday], [], [Friday, Monday], EventActor.System,
            Instant.FromUtc(2026, 9, 1, 0, 0), [new TeamDayOff(Friday, 50)], [new TeamDayOff(Friday, 25)]);

        // Act
        var roundTripped = JsonSerializer.Deserialize<SprintTeamDaysOffChangedEvent>(JsonSerializer.Serialize(original, Options), Options)!;

        // Assert
        roundTripped.TeamDaysOff.Should().Equal(Friday, Monday);
        roundTripped.PartDaysOff.Should().Equal(new TeamDayOff(Friday, 50));
        roundTripped.PreviousPartDaysOff.Should().Equal(new TeamDayOff(Friday, 25));
    }

    [Fact]
    public void APayloadWrittenAs1_0_ReadsAsFullDaysOff()
    {
        // Arrange — the shape before part days: no PartDaysOff or PreviousPartDaysOff
        var current = new SprintTeamDaysOffChangedEvent(Guid.CreateVersion7(), 9, [Friday], [], [Friday], EventActor.System,
            Instant.FromUtc(2026, 9, 1, 0, 0));
        var payload = JsonNode.Parse(JsonSerializer.Serialize(current, Options))!.AsObject();
        payload.Remove(nameof(SprintTeamDaysOffChangedEvent.PartDaysOff));
        payload.Remove(nameof(SprintTeamDaysOffChangedEvent.PreviousPartDaysOff));
        payload[nameof(DomainEvent.EventVersion)] = "1.0";

        // Act
        var read = JsonSerializer.Deserialize<SprintTeamDaysOffChangedEvent>(payload.ToJsonString(), Options)!;

        // Assert
        read.TeamDaysOff.Should().Equal(Friday);
        read.PartDaysOff.Should().BeEmpty();
        read.PreviousPartDaysOff.Should().BeEmpty();
    }
}
