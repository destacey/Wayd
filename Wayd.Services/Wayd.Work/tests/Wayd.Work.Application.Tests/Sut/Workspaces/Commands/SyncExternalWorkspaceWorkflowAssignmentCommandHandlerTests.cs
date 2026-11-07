using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;
using Wayd.Tests.Shared.Infrastructure;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.Workspaces.Commands;
using Wayd.Work.Domain;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Workspaces.Commands;

public sealed class SyncExternalWorkspaceWorkflowAssignmentCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 7, 9, 0);

    private readonly FakeWorkDbContext _dbContext = new();
    private readonly List<StatusWorkflowScheme> _schemes = [];
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<IStatusWorkflowDbContext> _statusWorkflowDbContext = new();
    private readonly Mock<IScopedWorkflowAssignments> _workflowAssignments = new();
    private readonly SyncExternalWorkspaceWorkflowAssignmentCommandHandler _handler;

    public SyncExternalWorkspaceWorkflowAssignmentCommandHandlerTests()
    {
        WorkWorkflowOwners.Register();

        _statusWorkflowDbContext.Setup(c => c.StatusWorkflowSchemes).Returns(() => _schemes.AsDbSet());
        _workflowAssignments
            .Setup(a => a.Unassign(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Guid.NewGuid()));

        _handler = new SyncExternalWorkspaceWorkflowAssignmentCommandHandler(
            _dbContext,
            _statusWorkflowDbContext.Object,
            _workflowAssignments.Object,
            new TestingDateTimeProvider(new NodaTime.Testing.FakeClock(Now)),
            _dispatcher.Object,
            NullLogger<SyncExternalWorkspaceWorkflowAssignmentCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    private Guid SeedWorkspaceOnProcess(Guid? processExternalId)
    {
        var process = new WorkProcessFaker().WithExternalId(processExternalId).Generate();
        _dbContext.AddWorkProcess(process);
        var workspace = new WorkspaceFaker().WithWorkProcessId(process.Id).Generate();
        _dbContext.AddWorkspace(workspace);

        return workspace.Id;
    }

    private void SeedScheme(string externalKey)
    {
        var workflow = StatusWorkflow.CreateManaged(
            "Agile - Story", WorkWorkflowOwners.WorkItem.Key, $"{externalKey}:Story", [new SourceStatus("New", "New", StatusCategory.Proposed)], EventActor.Sync(null), Now).Value;
        _schemes.Add(StatusWorkflowScheme.CreateManaged(WorkWorkflowOwners.WorkItem.Key, "Agile", externalKey, workflow, EventActor.Sync(null), Now).Value);
    }

    [Fact]
    public async Task Handle_ShouldAssignTheWorkspaceTheSchemeOfItsProcess()
    {
        // Arrange
        var processExternalId = Guid.NewGuid();
        var workspaceId = SeedWorkspaceOnProcess(processExternalId);
        SeedScheme($"work-process:{processExternalId:D}");

        // Act
        var result = await _handler.Handle(new SyncExternalWorkspaceWorkflowAssignmentCommand(workspaceId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dispatcher.Verify(d => d.Send(
            new SyncManagedWorkflowAssignmentCommand(WorkWorkflowOwners.WorkItem.Key, workspaceId, $"work-process:{processExternalId:D}"),
            It.IsAny<CancellationToken>()), Times.Once);
        _workflowAssignments.Verify(a => a.Unassign(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldUnassignTheWorkspace_WhenItsProcessHasNoScheme(bool processHasExternalId)
    {
        // Arrange
        var workspaceId = SeedWorkspaceOnProcess(processHasExternalId ? Guid.NewGuid() : null);
        SeedScheme($"work-process:{Guid.NewGuid():D}");

        // Act
        var result = await _handler.Handle(new SyncExternalWorkspaceWorkflowAssignmentCommand(workspaceId), TestContext.Current.CancellationToken);

        // Assert
        // A workspace that moved here keeps no scheme of the process it left; this process's own sync
        // assigns it once a scheme exists.
        result.IsSuccess.Should().BeTrue();
        _workflowAssignments.Verify(a => a.Unassign(
            WorkWorkflowOwners.WorkItem.Key, workspaceId, It.IsAny<EventActor>(), Now, It.IsAny<CancellationToken>()), Times.Once);
        _statusWorkflowDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSaveNothing_WhenAWorkspaceWithNoSchemeHasNoAssignmentToRemove()
    {
        // Arrange
        var workspaceId = SeedWorkspaceOnProcess(Guid.NewGuid());
        _workflowAssignments
            .Setup(a => a.Unassign(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(false));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkspaceWorkflowAssignmentCommand(workspaceId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _statusWorkflowDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheWorkspaceCannotBeUnassigned()
    {
        // Arrange
        var workspaceId = SeedWorkspaceOnProcess(Guid.NewGuid());
        _workflowAssignments
            .Setup(a => a.Unassign(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<bool>("The organization's workflow assignment cannot be removed."));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkspaceWorkflowAssignmentCommand(workspaceId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _statusWorkflowDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreASchemeOfAnotherOwnerType_WithTheSameKey()
    {
        // Arrange
        var processExternalId = Guid.NewGuid();
        var workspaceId = SeedWorkspaceOnProcess(processExternalId);
        var other = _schemes.Count;
        SeedScheme($"work-process:{processExternalId:D}");
        _schemes[other].SetPrivate(s => s.OwnerType, "test.other-owner");

        // Act
        var result = await _handler.Handle(new SyncExternalWorkspaceWorkflowAssignmentCommand(workspaceId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _workflowAssignments.Verify(a => a.Unassign(
            WorkWorkflowOwners.WorkItem.Key, workspaceId, It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheAssignmentIsRefused()
    {
        // Arrange
        var processExternalId = Guid.NewGuid();
        var workspaceId = SeedWorkspaceOnProcess(processExternalId);
        SeedScheme($"work-process:{processExternalId:D}");
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>("Only a published workflow can be assigned."));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkspaceWorkflowAssignmentCommand(workspaceId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a published workflow can be assigned.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheWorkspaceDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(new SyncExternalWorkspaceWorkflowAssignmentCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
