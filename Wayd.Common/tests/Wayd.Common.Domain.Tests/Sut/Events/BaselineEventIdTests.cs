using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Events;

public sealed class BaselineEventIdTests
{
    [Fact]
    public void For_TheSameRecord_IsAlwaysTheSameId()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();

        // Act
        var first = BaselineEventId.For("Project", projectId);
        var second = BaselineEventId.For("Project", projectId);

        // Assert
        first.Should().Be(second);
    }

    [Fact]
    public void For_ARecordOfAnotherTypeWithTheSameId_IsADifferentId()
    {
        // Arrange — a replica shares its source record's id
        var id = Guid.CreateVersion7();

        // Act
        var project = BaselineEventId.For("Project", id);
        var workProject = BaselineEventId.For("WorkProject", id);

        // Assert
        project.Should().NotBe(workProject);
    }

    [Fact]
    public void For_IsNeverTheRecordsOwnId()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        var eventId = BaselineEventId.For("Project", id);

        // Assert
        eventId.Should().NotBe(id);
    }
}
