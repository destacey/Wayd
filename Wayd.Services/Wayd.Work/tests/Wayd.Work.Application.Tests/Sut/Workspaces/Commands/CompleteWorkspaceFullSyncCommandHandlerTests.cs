using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.Workspaces.Commands;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Workspaces.Commands;

public sealed class CompleteWorkspaceFullSyncCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _fakeWorkDbContext = new();
    private readonly Mock<ILogger<CompleteWorkspaceFullSyncCommandHandler>> _logger = new();
    private readonly CompleteWorkspaceFullSyncCommandHandler _handler;

    public CompleteWorkspaceFullSyncCommandHandlerTests()
    {
        _handler = new CompleteWorkspaceFullSyncCommandHandler(_fakeWorkDbContext, _logger.Object);
    }

    public void Dispose()
    {
        _fakeWorkDbContext.Dispose();
    }

    [Fact]
    public async Task Handle_ClearsTheNeedForAFullSync()
    {
        // Arrange
        var workspace = new WorkspaceFaker().AsExternal().WithNeedsFullSync(true).Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);

        // Act
        var result = await _handler.Handle(new CompleteWorkspaceFullSyncCommand(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.NeedsFullSync.Should().BeFalse();
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenNoFullSyncWasNeeded_SavesNothing()
    {
        // Arrange
        var workspace = new WorkspaceFaker().AsExternal().WithNeedsFullSync(false).Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);

        // Act
        var result = await _handler.Handle(new CompleteWorkspaceFullSyncCommand(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Handle_WarnsOfItemsStillWithoutAStatus(bool itemWithoutStatus, int warnings)
    {
        // Arrange
        var workspace = new WorkspaceFaker().AsExternal().WithNeedsFullSync(true).Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);
        var faker = new WorkItemFaker(workspace.Id);
        if (itemWithoutStatus)
            faker = faker.WithNoEngineStatus();
        _fakeWorkDbContext.AddWorkItem(faker.Generate());

        // Act
        var result = await _handler.Handle(new CompleteWorkspaceFullSyncCommand(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.NeedsFullSync.Should().BeFalse("a sync that cannot resolve those items would not resolve them in full either");
        _logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(warnings));
    }

    [Fact]
    public async Task Handle_ForAnUnmanagedWorkspace_Fails()
    {
        // Arrange
        var workspace = new WorkspaceFaker().WithNeedsFullSync(true).Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);

        // Act
        var result = await _handler.Handle(new CompleteWorkspaceFullSyncCommand(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        workspace.NeedsFullSync.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAWorkspaceThatDoesNotExist_Fails()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new CompleteWorkspaceFullSyncCommand(workspaceId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(0);
    }
}
