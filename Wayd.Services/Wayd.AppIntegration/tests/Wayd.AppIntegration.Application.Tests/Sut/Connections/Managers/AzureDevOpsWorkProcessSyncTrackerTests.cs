using FluentAssertions;
using Wayd.AppIntegration.Application.Connections.Managers;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Managers;

public class AzureDevOpsWorkProcessSyncTrackerTests
{
    private readonly AzureDevOpsWorkProcessSyncTracker _sut = new();

    [Fact]
    public void TryMarkSynced_IsTrueOnlyTheFirstTimeInARun()
    {
        // Arrange
        var syncId = Guid.CreateVersion7();
        var processId = Guid.CreateVersion7();

        // Act
        var first = _sut.TryMarkSynced(syncId, processId);
        var second = _sut.TryMarkSynced(syncId, processId);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public void TryMarkSynced_TreatsEachRunSeparately()
    {
        // Arrange
        var processId = Guid.CreateVersion7();
        _sut.TryMarkSynced(Guid.CreateVersion7(), processId);

        // Act
        var inNewRun = _sut.TryMarkSynced(Guid.CreateVersion7(), processId);

        // Assert
        inNewRun.Should().BeTrue();
    }

    [Fact]
    public void TryMarkSynced_ForgetsTheOldestRunsOnceItHoldsTwenty()
    {
        // Arrange
        var processId = Guid.CreateVersion7();
        var oldestRun = Guid.CreateVersion7();
        _sut.TryMarkSynced(oldestRun, processId);
        for (var i = 0; i < 20; i++)
            _sut.TryMarkSynced(Guid.CreateVersion7(), processId);

        // Act
        var afterBeingForgotten = _sut.TryMarkSynced(oldestRun, processId);

        // Assert
        afterBeingForgotten.Should().BeTrue();
    }

    [Fact]
    public void Forget_LetsTheRunSyncTheProcessAgain()
    {
        // Arrange
        var syncId = Guid.CreateVersion7();
        var processId = Guid.CreateVersion7();
        var otherProcessId = Guid.CreateVersion7();
        _sut.TryMarkSynced(syncId, processId);
        _sut.TryMarkSynced(syncId, otherProcessId);

        // Act
        _sut.Forget(syncId, processId);

        // Assert
        _sut.TryMarkSynced(syncId, processId).Should().BeTrue();
        _sut.TryMarkSynced(syncId, otherProcessId).Should().BeFalse("only the forgotten process is taken back");
    }

    [Fact]
    public void Forget_IgnoresARunItHasNotSeen()
    {
        // Arrange
        var processId = Guid.CreateVersion7();

        // Act
        var forget = () => _sut.Forget(Guid.CreateVersion7(), processId);

        // Assert
        forget.Should().NotThrow();
    }
}
