using FluentAssertions;
using NodaTime;
using Wayd.Web.Api.Models.Planning.Roadmaps;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.Roadmaps;

public sealed class CreateRoadmapActivityRequestTests
{
    [Fact]
    public void ToCreateRoadmapItemCommand_CarriesTheOrder()
    {
        // Arrange
        var request = new CreateRoadmapActivityRequest
        {
            RoadmapId = Guid.NewGuid(),
            Name = "Beta programme",
            Start = new LocalDate(2026, 5, 1),
            End = new LocalDate(2026, 5, 29),
            Order = 2,
        };

        // Act
        var command = request.ToCreateRoadmapItemCommand();

        // Assert
        command.Order.Should().Be(2);
    }
}
