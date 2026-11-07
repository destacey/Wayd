using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared.Infrastructure;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses.Commands;
using Wayd.Work.Domain;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Commands;

public sealed class SyncExternalWorkProcessWorkflowsCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly List<StatusWorkflow> _engineWorkflows = [];
    private readonly List<object> _sent = [];
    private readonly Dictionary<string, Guid> _recordTypeIds = [];
    private readonly Dictionary<string, Guid> _workflowIds = [];
    private readonly SyncExternalWorkProcessWorkflowsCommandHandler _handler;

    public SyncExternalWorkProcessWorkflowsCommandHandlerTests()
    {
        WorkWorkflowOwners.Register();
        var statusWorkflowDbContext = new Mock<IStatusWorkflowDbContext>();
        statusWorkflowDbContext.Setup(c => c.StatusWorkflows).Returns(() => _engineWorkflows.AsDbSet());

        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedRecordTypeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedRecordTypeCommand c, CancellationToken _) => Sent(c, Id(_recordTypeIds, c.ExternalKey)));
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedWorkflowCommand c, CancellationToken _) => Sent(c, Id(_workflowIds, c.ExternalKey)));
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowSchemeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedWorkflowSchemeCommand c, CancellationToken _) => Sent(c, Guid.NewGuid()));
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedWorkflowAssignmentCommand c, CancellationToken _) => Sent(c, Guid.NewGuid()));

        _handler = new SyncExternalWorkProcessWorkflowsCommandHandler(
            _dbContext, statusWorkflowDbContext.Object, _dispatcher.Object, NullLogger<SyncExternalWorkProcessWorkflowsCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    private Result<Guid> Sent(object command, Guid id)
    {
        _sent.Add(command);
        return Result.Success(id);
    }

    private static Guid Id(Dictionary<string, Guid> ids, string key) =>
        ids.TryGetValue(key, out var id) ? id : ids[key] = Guid.NewGuid();

    private IEnumerable<T> SentOf<T>() => _sent.OfType<T>();

    private Guid SeedProcess(IExternalWorkProcessConfiguration process)
    {
        var workProcess = new WorkProcessFaker().AsExternal(process.Id).Generate();
        _dbContext.AddWorkProcess(workProcess);

        return workProcess.Id;
    }

    [Fact]
    public async Task Handle_ShouldKeepARecordTypeAndWorkflowPerWorkType_AndAssignEveryWorkspaceOnTheProcess()
    {
        // Arrange
        var process = ExternalProcess("Agile",
            WorkType("Story", ("New", StatusCategory.Proposed, 1, true), ("Active", StatusCategory.Active, 2, true), ("Closed", StatusCategory.Done, 3, true)),
            WorkType("Bug", ("Closed", StatusCategory.Done, 2, true), ("Old", StatusCategory.Active, 3, false), ("New", StatusCategory.Proposed, 1, true)));
        var workProcessId = SeedProcess(process);
        var onProcess = new[] { new WorkspaceFaker().WithWorkProcessId(workProcessId).Generate(), new WorkspaceFaker().WithWorkProcessId(workProcessId).Generate() };
        _dbContext.AddWorkspaces([.. onProcess, new WorkspaceFaker().WithWorkProcessId(Guid.NewGuid()).Generate()]);

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var ownerType = WorkWorkflowOwners.WorkItem.Key;

        SentOf<SyncManagedRecordTypeCommand>().Should().BeEquivalentTo(
            [new SyncManagedRecordTypeCommand(ownerType, "Bug", "Bug"), new SyncManagedRecordTypeCommand(ownerType, "Story", "Story")]);

        var bugWorkflow = SentOf<SyncManagedWorkflowCommand>().Single(c => c.Name == "Agile - Bug");
        bugWorkflow.OwnerType.Should().Be(ownerType);
        // Keyed by the type's reference name and each state's id, so a rename at the source renames them.
        bugWorkflow.ExternalKey.Should().Be($"work-process:{process.Id:D}:Custom.Bug");
        // The source's order, the hidden state included and marked, so history holding it still resolves.
        bugWorkflow.Statuses.Should().Equal(
            new SourceStatus("state-Bug-New", "New", StatusCategory.Proposed),
            new SourceStatus("state-Bug-Closed", "Closed", StatusCategory.Done),
            new SourceStatus("state-Bug-Old", "Old", StatusCategory.Active, IsHidden: true));
        SentOf<SyncManagedWorkflowCommand>().Single(c => c.Name == "Agile - Story").Statuses.Select(s => s.ExternalKey)
            .Should().Equal("state-Story-New", "state-Story-Active", "state-Story-Closed");

        var scheme = SentOf<SyncManagedWorkflowSchemeCommand>().Should().ContainSingle().Subject;
        (scheme.OwnerType, scheme.ExternalKey, scheme.Name).Should().Be((ownerType, $"work-process:{process.Id:D}", "Agile"));
        scheme.Entries.Should().BeEquivalentTo(
        [
            new ManagedSchemeEntry(_recordTypeIds["Bug"], _workflowIds[bugWorkflow.ExternalKey]),
            new ManagedSchemeEntry(_recordTypeIds["Story"], _workflowIds[$"work-process:{process.Id:D}:Custom.Story"]),
        ]);
        scheme.DefaultWorkflowId.Should().Be(_workflowIds[bugWorkflow.ExternalKey]);

        SentOf<SyncManagedWorkflowAssignmentCommand>().Should().BeEquivalentTo(
            onProcess.Select(w => new SyncManagedWorkflowAssignmentCommand(ownerType, w.Id, $"work-process:{process.Id:D}")));
    }

    [Fact]
    public async Task Handle_ShouldDefaultTheScheme_ToTheFirstActiveWorkTypeByName()
    {
        // Arrange
        var process = ExternalProcess("Agile",
            WorkType("Story", ("New", StatusCategory.Proposed, 1, true)),
            WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)) with { IsActive = false },
            WorkType("Task", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var scheme = SentOf<SyncManagedWorkflowSchemeCommand>().Should().ContainSingle().Subject;
        scheme.DefaultWorkflowId.Should().Be(_workflowIds[$"work-process:{process.Id:D}:Custom.Story"]);
        // A disabled type keeps its record type and its entry: its existing items still need a workflow.
        scheme.Entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAWorkTypeWithNoActiveStates()
    {
        // Arrange
        var process = ExternalProcess("Agile",
            WorkType("Story", ("New", StatusCategory.Proposed, 1, true)),
            WorkType("Retired", ("New", StatusCategory.Proposed, 1, false)));
        SeedProcess(process);

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        SentOf<SyncManagedRecordTypeCommand>().Select(c => c.Name).Should().Equal("Story");
        SentOf<SyncManagedWorkflowCommand>().Select(c => c.Name).Should().Equal("Agile - Story");
        SentOf<SyncManagedWorkflowSchemeCommand>().Should().ContainSingle().Which.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldStopBeforeTheScheme_WhenAWorkflowCannotBeSynced()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Story", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>("The source lists New twice."));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Story").And.Contain("The source lists New twice.");
        SentOf<SyncManagedWorkflowSchemeCommand>().Should().BeEmpty();
        SentOf<SyncManagedWorkflowAssignmentCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldDefaultToTheFirstType_WhenEveryTypeIsDisabled()
    {
        // Arrange
        var process = ExternalProcess("Agile",
            WorkType("Story", ("New", StatusCategory.Proposed, 1, true)) with { IsActive = false },
            WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)) with { IsActive = false });
        SeedProcess(process);

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        SentOf<SyncManagedWorkflowSchemeCommand>().Should().ContainSingle()
            .Which.DefaultWorkflowId.Should().Be(_workflowIds[$"work-process:{process.Id:D}:Custom.Bug"]);
    }

    [Fact]
    public async Task Handle_ShouldKeepNoScheme_ForAProcessWithNoWorkTypes()
    {
        // Arrange
        var process = ExternalProcess("Empty");
        var workProcessId = SeedProcess(process);
        _dbContext.AddWorkspace(new WorkspaceFaker().WithWorkProcessId(workProcessId).Generate());

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        // A scheme needs a default workflow; the workspace is left unassigned by its own sync.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldStopBeforeTheWorkflow_WhenARecordTypeCannotBeSynced()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedRecordTypeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>("A record type named 'Bug' already exists."));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("record type").And.Contain("A record type named 'Bug' already exists.");
        SentOf<SyncManagedWorkflowCommand>().Should().BeEmpty();
        SentOf<SyncManagedWorkflowSchemeCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAssignNoWorkspace_WhenTheSchemeCannotBeSynced()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)));
        var workProcessId = SeedProcess(process);
        _dbContext.AddWorkspace(new WorkspaceFaker().WithWorkProcessId(workProcessId).Generate());
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowSchemeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>("Status workflow not found."));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("scheme");
        SentOf<SyncManagedWorkflowAssignmentCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldStopAtTheFirstWorkspaceThatCannotBeAssigned()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)));
        var workProcessId = SeedProcess(process);
        _dbContext.AddWorkspaces([
            new WorkspaceFaker().WithWorkProcessId(workProcessId).Generate(),
            new WorkspaceFaker().WithWorkProcessId(workProcessId).Generate()]);
        var attempts = 0;
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++attempts == 1 ? Result.Failure<Guid>("Workflow scheme not found.") : Result.Success(Guid.NewGuid()));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        // Each assignment saves on its own, so the next sync finds what was done and carries on.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("workspace assignment");
        attempts.Should().Be(1);
    }

    /// <summary>A workflow the engine already keeps for one of the process's work types.</summary>
    private void SeedEngineWorkflow(IExternalWorkProcessConfiguration process, string workTypeName) =>
        _engineWorkflows.Add(StatusWorkflow.CreateManaged(
            $"{process.Name} - {workTypeName}",
            WorkWorkflowOwners.WorkItem.Key,
            $"work-process:{process.Id:D}:Custom.{workTypeName}",
            [new SourceStatus($"state-{workTypeName}-New", "New", StatusCategory.Proposed)],
            EventActor.Sync(null),
            NodaTime.Instant.FromUtc(2026, 11, 7, 9, 0)).Value);

    [Fact]
    public async Task Handle_ShouldNameTheRecordTypeAsWorkDoes_WhateverTheSourcesCasing()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("BUG", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        _dbContext.AddWorkType(new WorkTypeFaker().WithName("Bug").Generate());

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        // Work keeps one "Bug" for every spelling; two processes spelling it differently must not rename the
        // one record type back and forth on every sync.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        SentOf<SyncManagedRecordTypeCommand>().Should().ContainSingle()
            .Which.Should().Be(new SyncManagedRecordTypeCommand(WorkWorkflowOwners.WorkItem.Key, "Bug", "Bug"));
    }

    [Fact]
    public async Task Handle_ShouldKeepSyncingAWorkflow_OnceEveryStateOfItsTypeIsHidden()
    {
        // Arrange
        var process = ExternalProcess("Agile",
            WorkType("Story", ("New", StatusCategory.Proposed, 1, true)),
            WorkType("Bug", ("New", StatusCategory.Proposed, 1, false), ("Closed", StatusCategory.Done, 2, false)));
        SeedProcess(process);
        SeedEngineWorkflow(process, "Bug");

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        // Its statuses retire rather than stay as they were, and records holding them still resolve.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var bugWorkflowKey = $"work-process:{process.Id:D}:Custom.Bug";
        SentOf<SyncManagedWorkflowCommand>().Single(c => c.ExternalKey == bugWorkflowKey).Statuses.Should().OnlyContain(s => s.IsHidden);
        var scheme = SentOf<SyncManagedWorkflowSchemeCommand>().Should().ContainSingle().Subject;
        scheme.Entries.Should().HaveCount(2);
        // Bug sorts first, but no record could be given a status in its workflow.
        scheme.DefaultWorkflowId.Should().Be(_workflowIds[$"work-process:{process.Id:D}:Custom.Story"]);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProcessIsNotInWayd()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Story", ("New", StatusCategory.Proposed, 1, true)));

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _sent.Should().BeEmpty();
    }

    private static IExternalWorkProcessConfiguration ExternalProcess(string name, params TestWorkType[] workTypes)
    {
        var process = new Mock<IExternalWorkProcessConfiguration>();
        process.Setup(p => p.Id).Returns(Guid.CreateVersion7());
        process.Setup(p => p.Name).Returns(name);
        process.Setup(p => p.WorkTypes).Returns(workTypes.Cast<IExternalWorkTypeWorkflow>().ToList());
        return process.Object;
    }

    /// <summary>A work type whose reference name and state ids differ from its names, as in Azure DevOps.</summary>
    private static TestWorkType WorkType(string name, params (string Name, StatusCategory Category, int Order, bool IsActive)[] states) =>
        new(name, [.. states.Select(s => (IExternalWorkflowState)new TestState($"state-{name}-{s.Name}", s.Name, s.Category, s.Order, s.IsActive))]);

    private sealed record TestWorkType(string Name, IList<IExternalWorkflowState> WorkflowStates) : IExternalWorkTypeWorkflow
    {
        public string ExternalKey => $"Custom.{Name}";
        public string? Description => null;
        public string WorkTypeLevelId => "level";
        public bool IsActive { get; init; } = true;
    }

    private sealed record TestState(string ExternalKey, string StatusName, StatusCategory Category, int Order, bool IsActive) : IExternalWorkflowState;
}
