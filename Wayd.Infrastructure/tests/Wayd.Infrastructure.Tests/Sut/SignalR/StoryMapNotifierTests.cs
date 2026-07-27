using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wayd.Infrastructure.SignalR;
using Wayd.Planning.Application.StoryMaps.Dtos;

namespace Wayd.Infrastructure.Tests.Sut.SignalR;

/// <summary>
/// Covers the broadcast contract of <see cref="StoryMapNotifier"/>: handlers notify after committing,
/// so a hub failure must never surface as a command failure — the change is already saved, and an
/// error would have the client retry and duplicate it. Also pins the payload shape (map id first).
/// </summary>
public class StoryMapNotifierTests
{
    private static (StoryMapNotifier Notifier, Mock<IClientProxy> Group) BuildNotifier(Exception? sendThrows = null)
    {
        var group = new Mock<IClientProxy>();
        var setup = group.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()));

        if (sendThrows is null)
            setup.Returns(Task.CompletedTask);
        else
            setup.ThrowsAsync(sendThrows);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

        var hubContext = new Mock<IHubContext<StoryMapHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);

        var notifier = new StoryMapNotifier(hubContext.Object, Mock.Of<ILogger<StoryMapNotifier>>());
        return (notifier, group);
    }

    [Fact]
    public async Task Notify_WhenTheHubThrows_ShouldNotPropagate()
    {
        // Arrange — the change is already committed by the time the notifier runs.
        var (notifier, _) = BuildNotifier(new InvalidOperationException("hub is down"));

        // Act
        var task = new StoryMapTaskDto
        {
            Title = "A task",
            PersonaIds = [],
            Checklist = [],
        };

        var notify = async () => await notifier.NotifyTaskAdded(Guid.NewGuid(), task);

        // Assert — surfacing this would make the client retry and duplicate the task.
        await notify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Notify_WhenTheHubThrows_ShouldNotPropagateForPayloadlessEvents()
    {
        // Arrange
        var (notifier, _) = BuildNotifier(new InvalidOperationException("hub is down"));

        // Act
        var notify = async () => await notifier.NotifyMapArchived(Guid.NewGuid());

        // Assert
        await notify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Notify_ShouldBroadcastToTheMapsOwnGroup()
    {
        // Arrange
        var mapId = Guid.NewGuid();
        var group = new Mock<IClientProxy>();
        group.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

        var hubContext = new Mock<IHubContext<StoryMapHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);

        var notifier = new StoryMapNotifier(hubContext.Object, Mock.Of<ILogger<StoryMapNotifier>>());

        // Act
        await notifier.NotifyMapUpdated(mapId);

        // Assert — one group per map, so a broadcast reaches only that map's viewers.
        clients.Verify(c => c.Group(mapId.ToString()), Times.Once);
    }

    [Fact]
    public async Task Notify_ShouldLeadThePayloadWithTheMapId()
    {
        // Arrange
        var mapId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var (notifier, group) = BuildNotifier();

        // Act
        await notifier.NotifyGoalRenamed(mapId, goalId, "Renamed");

        // Assert — every event carries the map id ahead of its own arguments.
        group.Verify(
            p => p.SendCoreAsync(
                "GoalRenamed",
                It.Is<object?[]>(args =>
                    args.Length == 3
                    && (Guid)args[0]! == mapId
                    && (Guid)args[1]! == goalId
                    && (string)args[2]! == "Renamed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Notify_WithNoArguments_ShouldStillCarryTheMapId()
    {
        // Arrange
        var mapId = Guid.NewGuid();
        var (notifier, group) = BuildNotifier();

        // Act
        await notifier.NotifyMapDeleted(mapId);

        // Assert
        group.Verify(
            p => p.SendCoreAsync(
                "MapDeleted",
                It.Is<object?[]>(args => args.Length == 1 && (Guid)args[0]! == mapId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
