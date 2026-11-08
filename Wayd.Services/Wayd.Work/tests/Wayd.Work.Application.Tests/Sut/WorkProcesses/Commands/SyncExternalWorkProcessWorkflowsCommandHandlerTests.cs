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
using NodaTime;
using NodaTime.Testing;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Models;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Tests.Shared.Extensions;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Commands;

public sealed class SyncExternalWorkProcessWorkflowsCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 8, 9, 0);

    private readonly FakeWorkDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly List<StatusWorkflow> _engineWorkflows = [];
    private readonly List<object> _sent = [];
    private readonly Dictionary<string, Guid> _recordTypeIds = [];
    private readonly Dictionary<string, Guid> _workflowIds = [];
    private readonly Mock<IStatusWorkflowDbContext> _statusWorkflowDbContext = new();
    private readonly WorkTypeHierarchy _hierarchy = WorkTypeHierarchy.Initialize(Now);
    private readonly SyncExternalWorkProcessWorkflowsCommandHandler _handler;

    public SyncExternalWorkProcessWorkflowsCommandHandlerTests()
    {
        WorkWorkflowOwners.Register();
        _statusWorkflowDbContext.Setup(c => c.StatusWorkflows).Returns(() => _engineWorkflows.AsDbSet());

        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedRecordTypeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedRecordTypeCommand c, CancellationToken _) => Sent(c, Id(_recordTypeIds, c.ExternalKey)));
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedWorkflowCommand c, CancellationToken _) => Sent(c, Id(_workflowIds, c.ExternalKey)));
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowSchemeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedWorkflowSchemeCommand c, CancellationToken _) => Sent(c, Guid.NewGuid()));
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedWorkflowAssignmentCommand c, CancellationToken _) => Sent(c, Guid.NewGuid()));

        // Distinct ids, so a test can tell which level a new work type was given.
        var levelId = 1;
        foreach (var level in _hierarchy.Levels)
            level.SetPrivate(l => l.Id, levelId++);
        _dbContext.AddWorkTypeHierarchy(_hierarchy);

        _handler = new SyncExternalWorkProcessWorkflowsCommandHandler(
            _dbContext, _statusWorkflowDbContext.Object, _dispatcher.Object, new TestingDateTimeProvider(new FakeClock(Now)),
            NullLogger<SyncExternalWorkProcessWorkflowsCommandHandler>.Instance);
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

    /// <summary>
    /// A workflow the engine already keeps for one of the process's work types, which the workflow sync command
    /// reports back as the one it synced.
    /// </summary>
    private StatusWorkflow SeedEngineWorkflow(IExternalWorkProcessConfiguration process, string workTypeName, params SourceStatus[] statuses)
    {
        var key = $"work-process:{process.Id:D}:Custom.{workTypeName}";
        var workflow = StatusWorkflow.CreateManaged(
            $"{process.Name} - {workTypeName}",
            WorkWorkflowOwners.WorkItem.Key,
            key,
            statuses.Length > 0 ? statuses : [new SourceStatus($"state-{workTypeName}-New", "New", StatusCategory.Proposed)],
            EventActor.Sync(null),
            NodaTime.Instant.FromUtc(2026, 11, 7, 9, 0)).Value;
        // As saved: a workflow raises its events once it has a key.
        workflow.SetPrivate(w => w.Key, _engineWorkflows.Count + 1);
        _engineWorkflows.Add(workflow);
        _workflowIds[key] = workflow.Id;
        return workflow;
    }

    /// <summary>
    /// Has the workflow sync bring each seeded engine workflow into line with the source, as the engine command
    /// does, so a test can tell the statuses the sync creates from those already there.
    /// </summary>
    private void ApplyWorkflowSyncs() =>
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncManagedWorkflowCommand c, CancellationToken _) =>
            {
                var workflow = _engineWorkflows.Single(w => w.ExternalKey == c.ExternalKey);
                workflow.SyncFromSource(c.Name, c.Statuses, EventActor.Sync(null), Now).IsSuccess.Should().BeTrue();
                return Sent(c, workflow.Id);
            });

    [Fact]
    public async Task Handle_ShouldLinkTheWorkTypeToItsRecordType()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        var workType = new WorkTypeFaker().WithName("Bug").Generate();
        _dbContext.AddWorkType(workType);

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workType.RecordTypeId.Should().Be(_recordTypeIds["Bug"]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotSave_WhenTheWorkTypeIsAlreadyLinked()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        var workType = new WorkTypeFaker().WithName("Bug").Generate();
        workType.LinkRecordType(Id(_recordTypeIds, "Bug"));
        _dbContext.AddWorkType(workType);

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

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
    public async Task Handle_ShouldCreateTheWorkTypesWorkDoesNotHave_AtTheOtherLevel()
    {
        // Arrange — Work already has "bug", in another casing
        var process = ExternalProcess("Agile",
            WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)),
            WorkType("Story", ("New", StatusCategory.Proposed, 1, true)) with { Description = "A user's need." });
        SeedProcess(process);
        var bug = new WorkTypeFaker().WithName("bug").Generate();
        _dbContext.AddWorkType(bug);

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.WorkTypes.Select(t => t.Name).Should().BeEquivalentTo(["bug", "Story"]);
        var story = _dbContext.WorkTypes.Single(t => t.Name == "Story");
        story.LevelId.Should().Be(_hierarchy.Levels.Single(l => l.Tier == WorkTypeTier.Other).Id);
        story.Description.Should().Be("A user's need.");
        story.RecordTypeId.Should().Be(_recordTypeIds["Story"], "a type created by the sync is linked to its record type in the same run");
        _dbContext.SaveChangesCallCount.Should().Be(3, "the new types save once, then each type's link to its record type saves");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNoLevelCanTakeANewWorkType()
    {
        // Arrange
        using var dbContext = new FakeWorkDbContext();
        var handler = new SyncExternalWorkProcessWorkflowsCommandHandler(
            dbContext, _statusWorkflowDbContext.Object, _dispatcher.Object, new TestingDateTimeProvider(new FakeClock(Now)),
            NullLogger<SyncExternalWorkProcessWorkflowsCommandHandler>.Instance);
        var process = ExternalProcess("Agile", WorkType("Story", ("New", StatusCategory.Proposed, 1, true)));
        dbContext.AddWorkProcess(new WorkProcessFaker().AsExternal(process.Id).Generate());

        // Act
        var result = await handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("work type level");
        dbContext.SaveChangesCallCount.Should().Be(0);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldExplainACollision_WhenTheNewWorkTypesCannotBeSaved()
    {
        // Arrange — another sync of the same process created the type first
        var process = ExternalProcess("Agile", WorkType("Story", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        _dbContext.FailNextSaveWith = SaveFailures.UniqueKey();

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unable to save the new work types of Agile, most likely because another sync of the same process saved them at the same time. The next sync picks them up.");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _sent.Should().BeEmpty("nothing reaches the engine without the work types");
    }

    [Fact]
    public async Task Handle_ShouldNotClaimACollision_WhenTheNewWorkTypesFailForAnotherReason()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Story", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        _dbContext.FailNextSaveWith = SaveFailures.Truncation();

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unable to save the new work types of Agile.");
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldGiveAStatusItCreates_TheDescriptionWorksSharedStatusOfItsNameHad()
    {
        // Arrange — "Active" was already here with no description and "Closed" has one of its own; both are left
        // alone, since an existing status's description is the administrator's
        var process = ExternalProcess("Agile", WorkType("Bug",
            ("New", StatusCategory.Proposed, 1, true), ("Active", StatusCategory.Active, 2, true), ("Closed", StatusCategory.Done, 3, true)));
        SeedProcess(process);
        _dbContext.AddWorkType(new WorkTypeFaker().WithName("Bug").Generate());
        var workflow = SeedEngineWorkflow(process, "Bug",
            new SourceStatus("state-Bug-Active", "Active", StatusCategory.Active), new SourceStatus("state-Bug-Closed", "Closed", StatusCategory.Done));
        var active = workflow.Statuses.Single(s => s.Name == "Active");
        var closed = workflow.Statuses.Single(s => s.Name == "Closed");
        workflow.DescribeStatus(closed.Id, "Finished.", EventActor.System, Now);
        workflow.ClearDomainEvents();
        ApplyWorkflowSyncs();
        _dbContext.AddLegacyWorkStatusDescription(new LegacyWorkStatusDescriptionFaker().WithName("new").WithDescription("Not started yet.").Generate());
        _dbContext.AddLegacyWorkStatusDescription(new LegacyWorkStatusDescriptionFaker().WithName("Active").WithDescription("Being worked on.").Generate());
        _dbContext.AddLegacyWorkStatusDescription(new LegacyWorkStatusDescriptionFaker().WithName("Closed").WithDescription("Done and dusted.").Generate());

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        SentOf<SyncManagedWorkflowCommand>().Single().Statuses.Select(s => (s.Name, s.Description)).Should().Equal(
            ("New", "Not started yet."), ("Active", "Being worked on."), ("Closed", "Done and dusted."));
        var created = workflow.Statuses.Single(s => s.Name == "New");
        created.Description.Should().Be("Not started yet.");
        active.Description.Should().BeNull();
        closed.Description.Should().Be("Finished.");
        // In the same save as the status, and on the event that adds it, which the Activity log records.
        workflow.DomainEvents.OfType<WorkflowStatusAddedEvent>().Should().ContainSingle()
            .Which.Description.Should().Be("Not started yet.");
        workflow.DomainEvents.OfType<WorkflowStatusDescriptionChangedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLeaveADescriptionAnAdministratorCleared_Cleared()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        _dbContext.AddWorkType(new WorkTypeFaker().WithName("Bug").Generate());
        var workflow = SeedEngineWorkflow(process, "Bug", new SourceStatus("state-Bug-New", "New", StatusCategory.Proposed));
        var status = workflow.Statuses.Single();
        workflow.DescribeStatus(status.Id, "Not started yet.", EventActor.Sync(null), Now);
        workflow.DescribeStatus(status.Id, null, EventActor.User(Guid.NewGuid().ToString()), Now);
        workflow.ClearDomainEvents();
        ApplyWorkflowSyncs();
        _dbContext.AddLegacyWorkStatusDescription(new LegacyWorkStatusDescriptionFaker().WithName("New").WithDescription("Not started yet.").Generate());

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        status.Description.Should().BeNull();
        workflow.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSendNoDescription_WhenNoStatusMatchesOne()
    {
        // Arrange
        var process = ExternalProcess("Agile", WorkType("Bug", ("New", StatusCategory.Proposed, 1, true)));
        SeedProcess(process);
        _dbContext.AddWorkType(new WorkTypeFaker().WithName("Bug").Generate());
        var workflow = SeedEngineWorkflow(process, "Bug", new SourceStatus("state-Bug-New", "New", StatusCategory.Proposed));
        workflow.ClearDomainEvents();
        _dbContext.AddLegacyWorkStatusDescription(new LegacyWorkStatusDescriptionFaker().WithName("Resolved").WithDescription("Fixed.").Generate());

        // Act
        var result = await _handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        SentOf<SyncManagedWorkflowCommand>().Single().Statuses.Should().OnlyContain(s => s.Description == null);
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
        _dbContext.SaveChangesCallCount.Should().Be(0);
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
        public string? Description { get; init; }
        public string WorkTypeLevelId => "level";
        public bool IsActive { get; init; } = true;
    }

    private sealed record TestState(string ExternalKey, string StatusName, StatusCategory Category, int Order, bool IsActive) : IExternalWorkflowState;
}
