using FluentAssertions;
using Wayd.Tests.Shared.Data;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// Resolving a status from the workflow governing a scope, carrying the moves that workflow allows.
/// </summary>
public sealed class StatusResolverTests : StatusWorkflowHandlerTestBase
{
    private StatusResolver CreateSut() => new(DbContext);

    private StatusWorkflow SeedAssignedWorkflow(bool restricted)
    {
        var workflow = SeedWorkflow();
        var (proposed, notable, terminal) = (StatusId(workflow, "Proposed"), StatusId(workflow, "Notable"), StatusId(workflow, "Terminal"));

        if (restricted)
        {
            workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
            workflow.SetTransitions([new(proposed, notable), new(notable, terminal)], EventActor.System, Now);
        }

        workflow.Publish(EventActor.System, Now).IsSuccess.Should().BeTrue();

        var assignment = TestWorkflowAssignments.ForWorkflow(Widget.Key, null, workflow, EventActor.System, Now).Value;
        DbContext.AddWorkflowAssignment(assignment);

        return workflow;
    }

    private static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    [Fact]
    public async Task ForAlias_ShouldCarryTheStatusesAMoveIsAllowedFrom_WhenRestricted()
    {
        // Arrange
        var workflow = SeedAssignedWorkflow(restricted: true);

        // Act
        var result = await CreateSut().ForAlias(Widget.Key, null, TerminalAlias, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusId.Should().Be(StatusId(workflow, "Terminal"));
        result.Value.AllowedFrom.Should().BeEquivalentTo([StatusId(workflow, "Notable")]);
    }

    [Fact]
    public async Task ForAlias_ShouldAllowAnyMove_WhenTheWorkflowAllowsAny()
    {
        // Arrange
        SeedAssignedWorkflow(restricted: false);

        // Act
        var result = await CreateSut().ForAlias(Widget.Key, null, TerminalAlias, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AllowedFrom.Should().BeNull();
    }

    [Fact]
    public async Task Initial_ShouldCarryTheStatusesAMoveIsAllowedFrom_WhenRestricted()
    {
        // Arrange
        var workflow = SeedAssignedWorkflow(restricted: true);

        // Act
        var result = await CreateSut().Initial(Widget.Key, null, TestContext.Current.CancellationToken);

        // Assert
        // Nothing moves into the initial status here, which is harmless: a record entering it is exempt.
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusId.Should().Be(StatusId(workflow, "Proposed"));
        result.Value.AllowedFrom.Should().BeEmpty();
    }

    #region ForRecordType

    private static readonly WorkflowOwnerDescriptor Ticket = new(
        "test.resolver.ticket",
        "Ticket",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active), [TerminalAlias] = new("Terminal", StatusCategory.Done) },
        [NotableAlias, TerminalAlias],
        hasRecordTypes: true);

    /// <summary>
    /// A Ticket scheme sending Bugs to their own workflow, assigned organization-wide, and another sending
    /// nothing anywhere special, assigned to <paramref name="scopeId"/>.
    /// </summary>
    private (StatusWorkflow Standard, StatusWorkflow BugFlow, StatusWorkflow ScopedDefault, RecordType Bug, RecordType Story) SeedTicketSchemes(Guid scopeId)
    {
        WorkflowOwners.Register(Ticket);

        var standard = SeedWorkflow("Standard", publish: true, owner: Ticket);
        var bugFlow = SeedWorkflow("Bug Flow", publish: true, owner: Ticket);
        var scopedDefault = SeedWorkflow("Scoped Default", publish: true, owner: Ticket);
        var bug = RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value;
        var story = RecordType.Create(Ticket.Key, "Story", EventActor.System, Now).Value;

        var organization = StatusWorkflowScheme.Create(Ticket.Key, "Engineering", null, standard, EventActor.System, Now).Value;
        organization.SetEntry(bug, bugFlow, StatusRemap.AutoMap(standard, bugFlow).Value, EventActor.System, Now).IsSuccess.Should().BeTrue();
        DbContext.AddWorkflowAssignment(WorkflowAssignment.Create(Ticket.Key, null, organization, standard, EventActor.System, Now).Value);

        var scoped = StatusWorkflowScheme.Create(Ticket.Key, "Platform", null, scopedDefault, EventActor.System, Now).Value;
        DbContext.AddWorkflowAssignment(WorkflowAssignment.Create(Ticket.Key, scopeId, scoped, scopedDefault, EventActor.System, Now).Value);

        return (standard, bugFlow, scopedDefault, bug, story);
    }

    [Fact]
    public async Task ForRecordType_ShouldResolveDifferentWorkflowsForTwoTypesInOneScope()
    {
        // Arrange
        var (standard, bugFlow, _, bug, story) = SeedTicketSchemes(Guid.NewGuid());

        // Act
        var forBug = await CreateSut().ForRecordType(Ticket.Key, null, bug.Id, TestContext.Current.CancellationToken);
        var forStory = await CreateSut().ForRecordType(Ticket.Key, null, story.Id, TestContext.Current.CancellationToken);
        var untyped = await CreateSut().ForScope(Ticket.Key, null, TestContext.Current.CancellationToken);

        // Assert
        forBug.Value.Id.Should().Be(bugFlow.Id);
        forStory.Value.Id.Should().Be(standard.Id);
        untyped.Value.Id.Should().Be(standard.Id);
    }

    [Fact]
    public async Task ForRecordType_ShouldUseTheScopesOwnScheme_OverTheOrganizations()
    {
        // Arrange
        var scopeId = Guid.NewGuid();
        var (_, _, scopedDefault, bug, _) = SeedTicketSchemes(scopeId);

        // Act
        var result = await CreateSut().ForRecordType(Ticket.Key, scopeId, bug.Id, TestContext.Current.CancellationToken);

        // Assert
        // The scope's scheme has no Bug entry, so Bugs there follow its default, not the organization's Bug Flow.
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(scopedDefault.Id);
    }

    #endregion ForRecordType
}
