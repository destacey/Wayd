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
        var forBug = await CreateSut().ForRecordType(Ticket.Key, WorkflowScopeChain.Organization, bug.Id, TestContext.Current.CancellationToken);
        var forStory = await CreateSut().ForRecordType(Ticket.Key, WorkflowScopeChain.Organization, story.Id, TestContext.Current.CancellationToken);
        var untyped = await CreateSut().ForScope(Ticket.Key, null, TestContext.Current.CancellationToken);

        // Assert
        forBug.Value.Workflow.Id.Should().Be(bugFlow.Id);
        forStory.Value.Workflow.Id.Should().Be(standard.Id);
        untyped.Value.Id.Should().Be(standard.Id);
    }

    [Fact]
    public async Task ForRecordType_ShouldUseTheScopesOwnScheme_OverTheOrganizations()
    {
        // Arrange
        var scopeId = Guid.NewGuid();
        var (_, _, scopedDefault, bug, _) = SeedTicketSchemes(scopeId);

        // Act
        var result = await CreateSut().ForRecordType(Ticket.Key, WorkflowScopeChain.Of(scopeId), bug.Id, TestContext.Current.CancellationToken);

        // Assert
        // The scope's scheme has no Bug entry, so Bugs there follow its default, not the organization's Bug Flow.
        result.IsSuccess.Should().BeTrue();
        result.Value.Workflow.Id.Should().Be(scopedDefault.Id);
        result.Value.Source.Should().Be(new WorkflowSource(scopeId, IsRecordTypeEntry: false));
    }

    #endregion ForRecordType

    #region Scope chain

    /// <summary>
    /// Ticket workflows assigned at three levels: the organization's scheme sends Bugs to Org Bugs, the
    /// portfolio's sends Bugs to Portfolio Bugs, and the program's has only a default. The project has nothing.
    /// </summary>
    private sealed record ChainScenario(
        Guid ProjectId, Guid ProgramId, Guid PortfolioId,
        StatusWorkflow OrgDefault, StatusWorkflow OrgBugs, StatusWorkflow PortfolioDefault, StatusWorkflow PortfolioBugs,
        StatusWorkflow ProgramDefault, RecordType Bug, RecordType Story);

    private ChainScenario SeedChain()
    {
        WorkflowOwners.Register(Ticket);

        var bug = RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value;
        var story = RecordType.Create(Ticket.Key, "Story", EventActor.System, Now).Value;

        var orgDefault = SeedWorkflow("Org Default", publish: true, owner: Ticket);
        var orgBugs = SeedWorkflow("Org Bugs", publish: true, owner: Ticket);
        var portfolioDefault = SeedWorkflow("Portfolio Default", publish: true, owner: Ticket);
        var portfolioBugs = SeedWorkflow("Portfolio Bugs", publish: true, owner: Ticket);
        var programDefault = SeedWorkflow("Program Default", publish: true, owner: Ticket);

        var (projectId, programId, portfolioId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        AssignScheme(null, orgDefault, (bug, orgBugs));
        AssignScheme(portfolioId, portfolioDefault, (bug, portfolioBugs));
        AssignScheme(programId, programDefault);

        return new(projectId, programId, portfolioId, orgDefault, orgBugs, portfolioDefault, portfolioBugs, programDefault, bug, story);
    }

    private void AssignScheme(Guid? scopeId, StatusWorkflow defaultWorkflow, params (RecordType Type, StatusWorkflow Workflow)[] entries)
    {
        var scheme = StatusWorkflowScheme.Create(Ticket.Key, $"{defaultWorkflow.Name} scheme", null, defaultWorkflow, EventActor.System, Now).Value;
        foreach (var (type, workflow) in entries)
        {
            scheme.SetEntry(type, workflow, StatusRemap.AutoMap(defaultWorkflow, workflow).Value, EventActor.System, Now).IsSuccess.Should().BeTrue();
        }

        DbContext.AddWorkflowAssignment(WorkflowAssignment.Create(Ticket.Key, scopeId, scheme, defaultWorkflow, EventActor.System, Now).Value);
    }

    [Fact]
    public async Task ForRecordType_ShouldUseTheMostSpecificLevelWithAnAssignment()
    {
        // Arrange
        var s = SeedChain();
        var chain = WorkflowScopeChain.Of(s.ProjectId, s.ProgramId, s.PortfolioId);

        // Act
        var result = await CreateSut().ForRecordType(Ticket.Key, chain, s.Bug.Id, TestContext.Current.CancellationToken);

        // Assert
        // The program's default beats the portfolio's Bug entry: each level's default comes before a broader level.
        result.IsSuccess.Should().BeTrue();
        result.Value.Workflow.Id.Should().Be(s.ProgramDefault.Id);
        result.Value.Source.Should().Be(new WorkflowSource(s.ProgramId, IsRecordTypeEntry: false));
    }

    [Fact]
    public async Task ForRecordType_ShouldPreferTheTypesEntry_OverTheDefault_AtALevel()
    {
        // Arrange
        var s = SeedChain();
        var chain = WorkflowScopeChain.Of(s.ProjectId, s.PortfolioId);

        // Act
        var forBug = await CreateSut().ForRecordType(Ticket.Key, chain, s.Bug.Id, TestContext.Current.CancellationToken);
        var forStory = await CreateSut().ForRecordType(Ticket.Key, chain, s.Story.Id, TestContext.Current.CancellationToken);

        // Assert
        forBug.Value.Workflow.Id.Should().Be(s.PortfolioBugs.Id);
        forBug.Value.Source.Should().Be(new WorkflowSource(s.PortfolioId, IsRecordTypeEntry: true));
        forStory.Value.Workflow.Id.Should().Be(s.PortfolioDefault.Id);
        forStory.Value.Source.Should().Be(new WorkflowSource(s.PortfolioId, IsRecordTypeEntry: false));
    }

    [Fact]
    public async Task ForRecordType_ShouldPreferANarrowerLevelsTypeEntry_OverABroaderLevelsTypeEntry()
    {
        // Arrange
        var s = SeedChain();
        var projectBugs = SeedWorkflow("Project Bugs", publish: true, owner: Ticket);
        AssignScheme(s.ProjectId, s.ProgramDefault, (s.Bug, projectBugs));
        var chain = WorkflowScopeChain.Of(s.ProjectId, s.PortfolioId);

        // Act
        var result = await CreateSut().ForRecordType(Ticket.Key, chain, s.Bug.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Workflow.Id.Should().Be(projectBugs.Id);
        result.Value.Source.Should().Be(new WorkflowSource(s.ProjectId, IsRecordTypeEntry: true));
    }

    [Fact]
    public async Task ForScope_ShouldResolveAsAChainOfThatOneScope()
    {
        // Arrange
        var s = SeedChain();

        // Act
        var single = await CreateSut().ForScope(Ticket.Key, s.PortfolioId, TestContext.Current.CancellationToken);
        var chained = await CreateSut().ForRecordType(
            Ticket.Key, WorkflowScopeChain.ForScope(s.PortfolioId), null, TestContext.Current.CancellationToken);

        // Assert
        single.Value.Id.Should().Be(s.PortfolioDefault.Id);
        chained.Value.Workflow.Id.Should().Be(single.Value.Id);
    }

    [Fact]
    public async Task ForRecordType_ShouldFallBackToTheOrganization_WhenNoLevelHasAnAssignment()
    {
        // Arrange
        var s = SeedChain();
        var chain = WorkflowScopeChain.Of(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var forBug = await CreateSut().ForRecordType(Ticket.Key, chain, s.Bug.Id, TestContext.Current.CancellationToken);
        var forStory = await CreateSut().ForRecordType(Ticket.Key, chain, s.Story.Id, TestContext.Current.CancellationToken);

        // Assert
        forBug.Value.Workflow.Id.Should().Be(s.OrgBugs.Id);
        forBug.Value.Source.IsOrganization.Should().BeTrue();
        forStory.Value.Workflow.Id.Should().Be(s.OrgDefault.Id);
        forStory.Value.Source.Should().Be(new WorkflowSource(null, IsRecordTypeEntry: false));
    }

    [Fact]
    public async Task ForRecordType_ShouldIgnoreAssignmentsOfScopesOutsideTheChain()
    {
        // Arrange
        var s = SeedChain();

        // Act
        // The portfolio and program have assignments, but this record sits in neither.
        var result = await CreateSut().ForRecordType(Ticket.Key, WorkflowScopeChain.Of(s.ProjectId), s.Bug.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Workflow.Id.Should().Be(s.OrgBugs.Id);
        result.Value.Source.Should().Be(new WorkflowSource(null, IsRecordTypeEntry: true));
    }

    [Fact]
    public async Task ForRecordType_ShouldFail_WhenNothingIsAssignedAnywhere()
    {
        // Arrange
        WorkflowOwners.Register(Ticket);

        // Act
        var result = await CreateSut().ForRecordType(Ticket.Key, WorkflowScopeChain.Of(Guid.NewGuid()), null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No workflow is assigned");
    }

    #endregion Scope chain
}
