using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>The sync keeping a managed workflow in line with its source.</summary>
public sealed class SyncManagedWorkflowCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.sync.item",
        "Item",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active) },
        [],
        [StatusCategory.Proposed, StatusCategory.Done],
        hasManagedWorkflows: true);

    private static readonly SourceStatus New = new("new", "New", StatusCategory.Proposed);
    private static readonly SourceStatus Active = new("active", "Active", StatusCategory.Active);
    private static readonly SourceStatus Closed = new("closed", "Closed", StatusCategory.Done);

    public SyncManagedWorkflowCommandHandlerTests() => WorkflowOwners.Register(Synced);

    private SyncManagedWorkflowCommandHandler CreateSut() =>
        new(DbContext, DateTimeProvider.Object, Logger<SyncManagedWorkflowCommandHandler>(), OwnerAvailability);

    private static SyncManagedWorkflowCommand Command(params SourceStatus[] statuses) =>
        new(Synced.Key, "conn-1:agile:bug", "Agile: Bug", statuses);

    private StatusWorkflow SeedManaged()
    {
        var workflow = StatusWorkflow.CreateManaged("Agile: Bug", Synced.Key, "conn-1:agile:bug", [New, Active, Closed], EventActor.Sync(null), Now).Value;
        workflow.ClearDomainEvents();
        DbContext.AddStatusWorkflow(workflow);

        return workflow;
    }

    [Fact]
    public async Task Handle_ShouldCreateAPublishedManagedWorkflow_TheFirstTime()
    {
        // Act
        var result = await CreateSut().Handle(Command(New, Active, Closed), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var workflow = DbContext.StatusWorkflows.Single(w => w.Id == result.Value);
        (workflow.Ownership, workflow.ExternalKey, workflow.State).Should().Be(
            (WorkflowOwnership.Managed, "conn-1:agile:bug", StatusWorkflowState.Published));
        workflow.Statuses.Select(s => s.ExternalKey).Should().Equal("new", "active", "closed");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCreateTheWorkflow_WhenTheSourceLacksARequiredCategory()
    {
        // Act
        var result = await CreateSut().Handle(Command(Active), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        DbContext.StatusWorkflows.Single(w => w.Id == result.Value).MissingRequiredCategories()
            .Should().Equal(StatusCategory.Proposed, StatusCategory.Done);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTheExistingWorkflow_ByItsExternalKey()
    {
        // Arrange
        var workflow = SeedManaged();
        var activeId = workflow.Statuses.Single(s => s.ExternalKey == "active").Id;

        // Act
        var result = await CreateSut().Handle(
            Command(New, new("active", "In Progress", StatusCategory.Active), new("resolved", "Resolved", StatusCategory.Done)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(workflow.Id);
        DbContext.StatusWorkflows.Should().ContainSingle();
        workflow.Statuses.Single(s => s.Id == activeId).Name.Should().Be("In Progress");
        workflow.Statuses.Single(s => s.ExternalKey == "closed").IsRetired.Should().BeTrue();
        workflow.ActiveStatuses.Select(s => s.ExternalKey).Should().Equal("new", "active", "resolved");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReinstateARetiredStatus_WhenTheSourceBringsItBack()
    {
        // Arrange
        var workflow = SeedManaged();
        var closedId = workflow.Statuses.Single(s => s.ExternalKey == "closed").Id;
        workflow.SyncFromSource("Agile: Bug", [New, Active], EventActor.Sync(null), Now).IsSuccess.Should().BeTrue();
        workflow.ClearDomainEvents();

        // Act
        var result = await CreateSut().Handle(Command(New, Active, Closed), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        workflow.Statuses.Should().HaveCount(3, "the returning status is matched by its key, not added again");
        workflow.Statuses.Single(s => s.Id == closedId).IsRetired.Should().BeFalse();
        workflow.ActiveStatuses.Select(s => s.Id).Should().EndWith(closedId);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheSourceListsAStatusTwice()
    {
        // Arrange
        var workflow = SeedManaged();

        // Act
        var result = await CreateSut().Handle(Command(New, New), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        workflow.Statuses.Should().OnlyContain(s => !s.IsRetired);
        workflow.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeWhoseWorkflowsAreNotSynced()
    {
        // Act
        var result = await CreateSut().Handle(
            new SyncManagedWorkflowCommand(Widget.Key, "conn-1:widget", "Widgets", [New]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Widget workflows are not kept by a sync.");
        DbContext.StatusWorkflows.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
