using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItems;
using Wayd.Work.Domain;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems;

public class WorkTypeStatusesTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 2, 9, 0);
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    private readonly Mock<IStatusResolver> _statusResolver = new();

    public WorkTypeStatusesTests() => WorkWorkflowOwners.Register();

    private static StatusWorkflow Workflow(params SourceStatus[] statuses) =>
        StatusWorkflow.CreateManaged("Bug workflow", WorkWorkflowOwners.WorkItem.Key, Guid.NewGuid().ToString(), statuses, EventActor.System, Now).Value;

    private void Resolves(Guid recordTypeId, StatusWorkflow workflow, bool isRecordTypeEntry = true) =>
        _statusResolver
            .Setup(r => r.ForRecordType(WorkWorkflowOwners.WorkItem.Key,
                It.Is<WorkflowScopeChain>(c => c.ScopeIds.SequenceEqual(new[] { WorkspaceId })), recordTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedWorkflow(workflow, new WorkflowSource(WorkspaceId, isRecordTypeEntry)));

    [Fact]
    public async Task Find_MatchesAStatusOfTheTypesWorkflowIgnoringCase()
    {
        // Arrange
        var recordTypeId = Guid.NewGuid();
        var workflow = Workflow(new SourceStatus("1", "Active", StatusCategory.Active), new SourceStatus("2", "Closed", StatusCategory.Done));
        Resolves(recordTypeId, workflow);

        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, recordTypeId)], TestContext.Current.CancellationToken);

        // Assert
        var closed = statuses.Value.Find(7, " closed ");
        closed.Should().NotBeNull();
        closed!.StatusId.Should().Be(workflow.Statuses.Single(s => s.Name == "Closed").Id);
        closed.WorkflowId.Should().Be(workflow.Id);
        closed.Category.Should().Be(StatusCategory.Done);
        statuses.Value.Find(7, "Resolved").Should().BeNull();
        statuses.Value.Find(8, "Closed").Should().BeNull("another type's workflow is never searched");
    }

    [Fact]
    public async Task Find_MatchesARetiredStatus()
    {
        // Arrange
        var recordTypeId = Guid.NewGuid();
        var workflow = Workflow(
            new SourceStatus("1", "Active", StatusCategory.Active),
            new SourceStatus("2", "Resolved", StatusCategory.Done, IsHidden: true));
        Resolves(recordTypeId, workflow);

        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, recordTypeId)], TestContext.Current.CancellationToken);

        // Assert
        var resolved = statuses.Value.Find(7, "Resolved");
        resolved!.StatusId.Should().Be(workflow.Statuses.Single(s => s.Name == "Resolved").Id);
        resolved.Category.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public async Task Find_PrefersAnActiveStatusOverARetiredOneOfTheSameName()
    {
        // Arrange — the source dropped "Resolved", which the workflow retires, then added a new state of that name
        var recordTypeId = Guid.NewGuid();
        var workflow = Workflow(
            new SourceStatus("1", "Active", StatusCategory.Active),
            new SourceStatus("2", "Resolved", StatusCategory.Done));
        workflow.SyncFromSource(workflow.Name,
        [
            new SourceStatus("1", "Active", StatusCategory.Active),
            new SourceStatus("3", "Resolved", StatusCategory.Removed),
        ], EventActor.System, Now).IsSuccess.Should().BeTrue();
        workflow.Statuses.Count(s => s.Name == "Resolved").Should().Be(2, "the case under test needs both");
        Resolves(recordTypeId, workflow);

        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, recordTypeId)], TestContext.Current.CancellationToken);

        // Assert
        var active = workflow.Statuses.Single(s => s.ExternalKey == "3");
        statuses.Value.Find(7, "Resolved")!.StatusId.Should().Be(active.Id);
    }

    [Fact]
    public async Task Find_MatchesANameTheSourceRenamedTheStatusFrom()
    {
        // Arrange — revisions written before the rename still report "Ready"
        var recordTypeId = Guid.NewGuid();
        var workflow = Workflow(new SourceStatus("1", "Ready", StatusCategory.Proposed), new SourceStatus("2", "Closed", StatusCategory.Done));
        workflow.SyncFromSource("Bug workflow", [new SourceStatus("1", "Approved", StatusCategory.Proposed), new SourceStatus("2", "Closed", StatusCategory.Done)], EventActor.Sync(null), Now);
        Resolves(recordTypeId, workflow);

        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, recordTypeId)], TestContext.Current.CancellationToken);

        // Assert
        var approved = workflow.Statuses.Single(s => s.ExternalKey == "1");
        statuses.Value.Find(7, "ready")!.StatusId.Should().Be(approved.Id);
        statuses.Value.Find(7, "Approved")!.StatusId.Should().Be(approved.Id);
    }

    [Fact]
    public async Task Find_PrefersAStatusCurrentlyNamedSo_OverOneFormerlyNamedSo()
    {
        // Arrange — "Ready" became "Approved", then the source renamed another state to "Ready"
        var recordTypeId = Guid.NewGuid();
        var workflow = Workflow(new SourceStatus("1", "Ready", StatusCategory.Proposed), new SourceStatus("2", "Queued", StatusCategory.Active));
        workflow.SyncFromSource("Bug workflow", [new SourceStatus("1", "Approved", StatusCategory.Proposed), new SourceStatus("2", "Queued", StatusCategory.Active)], EventActor.Sync(null), Now);
        workflow.SyncFromSource("Bug workflow", [new SourceStatus("1", "Approved", StatusCategory.Proposed), new SourceStatus("2", "Ready", StatusCategory.Active)], EventActor.Sync(null), Now);
        Resolves(recordTypeId, workflow);

        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, recordTypeId)], TestContext.Current.CancellationToken);

        // Assert
        statuses.Value.Find(7, "Ready")!.StatusId.Should().Be(workflow.Statuses.Single(s => s.ExternalKey == "2").Id);
        statuses.Value.Find(7, "Queued")!.StatusId.Should().Be(workflow.Statuses.Single(s => s.ExternalKey == "2").Id);
    }

    [Fact]
    public async Task Load_ATypeWithoutAnEntryOfItsOwn_GetsNoStatuses()
    {
        // Arrange — the scheme's default belongs to another type
        var recordTypeId = Guid.NewGuid();
        Resolves(recordTypeId, Workflow(new SourceStatus("1", "Active", StatusCategory.Active)), isRecordTypeEntry: false);

        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, recordTypeId)], TestContext.Current.CancellationToken);

        // Assert
        statuses.IsSuccess.Should().BeTrue();
        statuses.Value.Find(7, "Active").Should().BeNull();
    }

    [Fact]
    public async Task Load_ATypeWithNoRecordType_IsNotResolved()
    {
        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, null)], TestContext.Current.CancellationToken);

        // Assert
        statuses.IsSuccess.Should().BeTrue();
        statuses.Value.Find(7, "Active").Should().BeNull();
        _statusResolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Load_WhenTheWorkspaceHasNoWorkflow_Fails()
    {
        // Arrange
        var recordTypeId = Guid.NewGuid();
        _statusResolver
            .Setup(r => r.ForRecordType(It.IsAny<string>(), It.IsAny<WorkflowScopeChain>(), recordTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ResolvedWorkflow>("No workflow is assigned for Work Item."));

        // Act
        var statuses = await WorkTypeStatuses.Load(_statusResolver.Object, WorkspaceId, [(7, recordTypeId)], TestContext.Current.CancellationToken);

        // Assert
        statuses.IsFailure.Should().BeTrue();
        statuses.Error.Should().Contain("No workflow is assigned for Work Item.");
    }
}
