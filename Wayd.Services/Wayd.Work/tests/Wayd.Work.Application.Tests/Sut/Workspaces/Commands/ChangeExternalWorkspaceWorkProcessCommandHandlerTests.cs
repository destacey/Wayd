using FluentAssertions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.Workspaces.Commands;
using Wayd.Work.Domain.Tests.Data;
using Moq;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Workspaces.Commands;

public class ChangeExternalWorkspaceWorkProcessCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _fakeWorkDbContext;
    private readonly ChangeExternalWorkspaceWorkProcessCommandHandler _handler;
    private readonly WorkspaceFaker _workspaceFaker;
    private readonly WorkProcessFaker _workProcessFaker;

    public ChangeExternalWorkspaceWorkProcessCommandHandlerTests()
    {
        _fakeWorkDbContext = new FakeWorkDbContext();
        var dateTimeProvider = new TestingDateTimeProvider(new DateTime(2026, 04, 12, 12, 0, 0));
        var logger = Mock.Of<ILogger<ChangeExternalWorkspaceWorkProcessCommandHandler>>();
        _handler = new ChangeExternalWorkspaceWorkProcessCommandHandler(_fakeWorkDbContext, dateTimeProvider, logger);
        _workspaceFaker = new WorkspaceFaker();
        _workProcessFaker = new WorkProcessFaker();
    }

    public void Dispose()
    {
        _fakeWorkDbContext.Dispose();
    }

    [Fact]
    public async Task Handle_WhenWorkspaceNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new ChangeExternalWorkspaceWorkProcessCommand(Guid.CreateVersion7(), Guid.CreateVersion7());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNewWorkProcessNotFound_ReturnsFailure()
    {
        // Arrange
        var oldProcessExternalId = Guid.CreateVersion7();
        var oldProcess = _workProcessFaker.AsExternal(oldProcessExternalId).Generate();
        var workspace = _workspaceFaker.AsExternal().WithWorkProcessId(oldProcess.Id).Generate();

        _fakeWorkDbContext.AddWorkProcess(oldProcess);
        _fakeWorkDbContext.AddWorkspace(workspace);

        var newProcessExternalId = Guid.CreateVersion7(); // not in the db
        var command = new ChangeExternalWorkspaceWorkProcessCommand(workspace.Id, newProcessExternalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("not be initialized yet");
    }

    [Fact]
    public async Task Handle_WhenValid_ChangesWorkProcessAndSaves()
    {
        // Arrange
        var oldProcessExternalId = Guid.CreateVersion7();
        var newProcessExternalId = Guid.CreateVersion7();

        var oldProcess = _workProcessFaker.AsExternal(oldProcessExternalId).Generate();
        var newProcess = _workProcessFaker.AsExternal(newProcessExternalId).Generate();
        var workspace = _workspaceFaker.AsExternal().WithWorkProcessId(oldProcess.Id).Generate();

        _fakeWorkDbContext.AddWorkProcess(oldProcess);
        _fakeWorkDbContext.AddWorkProcess(newProcess);
        _fakeWorkDbContext.AddWorkspace(workspace);

        var command = new ChangeExternalWorkspaceWorkProcessCommand(workspace.Id, newProcessExternalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.WorkProcessId.Should().Be(newProcess.Id);
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSameProcess_ReturnsSuccessWithNoSave()
    {
        // Arrange
        var processExternalId = Guid.CreateVersion7();
        var process = _workProcessFaker.AsExternal(processExternalId).Generate();
        var workspace = _workspaceFaker.AsExternal().WithWorkProcessId(process.Id).Generate();

        _fakeWorkDbContext.AddWorkProcess(process);
        _fakeWorkDbContext.AddWorkspace(workspace);

        // The new external ID resolves to the same internal process
        var command = new ChangeExternalWorkspaceWorkProcessCommand(workspace.Id, processExternalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.WorkProcessId.Should().Be(process.Id);
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenOwnedWorkspace_ReturnsFailure()
    {
        // Arrange - default WorkspaceFaker creates an owned workspace
        var processExternalId = Guid.CreateVersion7();
        var process = _workProcessFaker.AsExternal(processExternalId).Generate();
        var workspace = _workspaceFaker.WithWorkProcessId(process.Id).Generate(); // owned, not external

        _fakeWorkDbContext.AddWorkProcess(process);
        _fakeWorkDbContext.AddWorkspace(workspace);

        var newProcessExternalId = Guid.CreateVersion7();
        var newProcess = _workProcessFaker.AsExternal(newProcessExternalId).Generate();
        _fakeWorkDbContext.AddWorkProcess(newProcess);

        var command = new ChangeExternalWorkspaceWorkProcessCommand(workspace.Id, newProcessExternalId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("non-managed");
    }
}
