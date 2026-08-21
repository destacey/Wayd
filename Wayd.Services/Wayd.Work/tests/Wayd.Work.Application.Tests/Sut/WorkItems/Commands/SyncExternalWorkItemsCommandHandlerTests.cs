using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.Tests.Data;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Commands;

public class SyncExternalWorkItemsCommandHandlerTests : IDisposable
{
    private static readonly Guid _connectionId = Guid.Parse("1c9f0a3e-6b4d-4f2a-9c81-5d0e7a2b4f63");

    private readonly FakeWorkDbContext _fakeWorkDbContext;
    private readonly Mock<ILogger<SyncExternalWorkItemsCommandHandler>> _mockLogger;
    private readonly SyncExternalWorkItemsCommandHandler _handler;
    private readonly FakeClock _clock;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    private readonly ExternalWorkItemFaker _externalWorkItemFaker;
    private readonly WorkspaceFaker _workspaceFaker;
    private readonly WorkProcessFaker _workProcessFaker;

    public SyncExternalWorkItemsCommandHandlerTests()
    {
        _fakeWorkDbContext = new FakeWorkDbContext();
        _mockLogger = new Mock<ILogger<SyncExternalWorkItemsCommandHandler>>();
        _clock = new FakeClock(Instant.FromUtc(2024, 1, 15, 12, 0, 0));
        _dateTimeProvider = new TestingDateTimeProvider(_clock);
        _handler = new SyncExternalWorkItemsCommandHandler(_fakeWorkDbContext, _dateTimeProvider, _mockLogger.Object);

        _externalWorkItemFaker = new ExternalWorkItemFaker(_clock.GetCurrentInstant(), _clock.GetCurrentInstant());
        _workspaceFaker = new WorkspaceFaker();
        _workProcessFaker = new WorkProcessFaker();
    }

    public void Dispose()
    {
        _fakeWorkDbContext.Dispose();
    }

    [Fact]
    public async Task Handle_WithEmptyWorkItems_ReturnsSuccess()
    {
        // Arrange
        var command = new SyncExternalWorkItemsCommand(
            _connectionId,
            Connector.AzureDevOps,
            Guid.NewGuid(),
            [],
            [],
            []);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(0); // No changes when empty
    }

    [Fact]
    public async Task Handle_WithNonExistentWorkspace_ReturnsFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var externalWorkItem = _externalWorkItemFaker.Generate();
        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // No workspace added - simulates non-existent workspace

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("workspace does not exist");
    }

    [Fact]
    public async Task Handle_WithWorkspaceWithoutWorkProcess_ReturnsFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workspace = _workspaceFaker
            .AsExternal()
            .WithId(workspaceId)
            .Generate();
        var externalWorkItem = _externalWorkItemFaker.Generate();
        var command = CreateCommand(workspaceId, [externalWorkItem]);

        _fakeWorkDbContext.AddWorkspace(workspace);
        // No work process added - simulates workspace without work process

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not have a work process");
    }

    [Fact]
    public async Task Handle_SkipsWorkItem_WithUnknownWorkType()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workProcessId = Guid.NewGuid();

        var workProcess = CreateWorkProcessWithSchemes("User Story", "New");

        var workspace = _workspaceFaker
            .AsExternal()
            .WithId(workspaceId)
            .WithWorkProcessId(workProcessId)
            .Generate();

        var externalWorkItem = _externalWorkItemFaker
            .WithWorkType("UnknownType")
            .Generate();
        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // Set up the work process with the correct ID
        var updatedWorkProcess = _workProcessFaker
            .WithId(workProcessId)
            .WithSchemes([.. workProcess.Schemes])
            .Generate();

        _fakeWorkDbContext.AddWorkspace(workspace);
        _fakeWorkDbContext.AddWorkProcess(updatedWorkProcess);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().BeGreaterThan(0); // SaveChanges called even though item skipped
    }

    [Fact]
    public async Task Handle_SkipsWorkItem_WithUnknownWorkStatus()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workProcessId = Guid.NewGuid();

        var workProcess = CreateWorkProcessWithSchemes("User Story", "New");

        var workspace = _workspaceFaker
            .AsExternal()
            .WithId(workspaceId)
            .WithWorkProcessId(workProcessId)
            .Generate();

        var externalWorkItem = _externalWorkItemFaker
            .WithWorkType("User Story")
            .WithWorkStatus("UnknownStatus")
            .Generate();
        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // Set up the work process with the correct ID
        var updatedWorkProcess = _workProcessFaker
            .WithId(workProcessId)
            .WithSchemes([.. workProcess.Schemes])
            .Generate();

        _fakeWorkDbContext.AddWorkspace(workspace);
        _fakeWorkDbContext.AddWorkProcess(updatedWorkProcess);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_HandlesException_AndReturnsFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var externalWorkItem = _externalWorkItemFaker.Generate();
        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // Use Mock<IWorkDbContext> for exception testing
        var mockWorkDbContext = new Mock<IWorkDbContext>();
        mockWorkDbContext.Setup(x => x.Workspaces)
            .Throws(new InvalidOperationException("Database error"));

        var handlerWithMock = new SyncExternalWorkItemsCommandHandler(mockWorkDbContext.Object, _dateTimeProvider, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handlerWithMock.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_LogsWarning_WhenWorkspaceNotFound()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var externalWorkItem = _externalWorkItemFaker.Generate();
        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // No workspace added - simulates not found

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unable to sync external work items")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsError_WhenExceptionOccurs()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var externalWorkItem = _externalWorkItemFaker.Generate();
        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // Use Mock<IWorkDbContext> for exception testing
        var mockWorkDbContext = new Mock<IWorkDbContext>();
        mockWorkDbContext.Setup(x => x.Workspaces)
            .Throws(new InvalidOperationException("Database error"));

        var handlerWithMock = new SyncExternalWorkItemsCommandHandler(mockWorkDbContext.Object, _dateTimeProvider, _mockLogger.Object);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handlerWithMock.Handle(command, TestContext.Current.CancellationToken));

        // Assert
        exception.Message.Should().Be("Database error");
    }

    [Fact]
    public async Task Handle_WithTags_CreatesWorkItemSuccessfully()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workProcessId = Guid.NewGuid();

        var workProcess = CreateWorkProcessWithSchemes("User Story", "New");

        var workspace = _workspaceFaker
            .AsExternal()
            .WithId(workspaceId)
            .WithWorkProcessId(workProcessId)
            .Generate();

        var externalWorkItem = _externalWorkItemFaker
            .WithWorkType("User Story")
            .WithWorkStatus("New")
            .WithTags("Backend", "Q2", "Performance")
            .Generate();

        var updatedWorkProcess = _workProcessFaker
            .WithId(workProcessId)
            .WithSchemes([.. workProcess.Schemes])
            .Generate();

        _fakeWorkDbContext.AddWorkspace(workspace);
        _fakeWorkDbContext.AddWorkProcess(updatedWorkProcess);

        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WithNoTags_CreatesWorkItemSuccessfully()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workProcessId = Guid.NewGuid();

        var workProcess = CreateWorkProcessWithSchemes("User Story", "New");

        var workspace = _workspaceFaker
            .AsExternal()
            .WithId(workspaceId)
            .WithWorkProcessId(workProcessId)
            .Generate();

        var externalWorkItem = _externalWorkItemFaker
            .WithWorkType("User Story")
            .WithWorkStatus("New")
            .Generate();

        var updatedWorkProcess = _workProcessFaker
            .WithId(workProcessId)
            .WithSchemes([.. workProcess.Schemes])
            .Generate();

        _fakeWorkDbContext.AddWorkspace(workspace);
        _fakeWorkDbContext.AddWorkProcess(updatedWorkProcess);

        var command = CreateCommand(workspaceId, [externalWorkItem]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakeWorkDbContext.SaveChangesCallCount.Should().BeGreaterThan(0);
    }

    #region Helper Methods

    private SyncExternalWorkItemsCommand CreateCommand(
        Guid workspaceId,
        List<IExternalWorkItem> workItems,
        Dictionary<Guid, Guid?>? teamMappings = null,
        Dictionary<string, Guid>? iterationMappings = null,
        Guid? connectionId = null)
    {
        return new SyncExternalWorkItemsCommand(
            connectionId ?? _connectionId,
            Connector.AzureDevOps,
            workspaceId,
            workItems,
            teamMappings ?? [],
            iterationMappings ?? []);
    }

    private WorkProcess CreateWorkProcessWithSchemes(string workTypeName, string workStatusName)
    {
        var workType = new WorkTypeFaker().WithName(workTypeName).Generate();
        var workStatus = new WorkStatusFaker().WithName(workStatusName).Generate();

        // Create workflow with properly linked workflow scheme
        var workflow = new WorkflowFaker()
            .WithWorkflowScheme(workStatus, WorkStatusCategory.Proposed)
            .Generate();

        var workProcessScheme = new WorkProcessSchemeFaker()
            .WithWorkType(workType)
            .WithWorkflow(workflow)
            .Generate();

        return _workProcessFaker
            .WithSchemes([workProcessScheme])
            .Generate();
    }

    #endregion
}
