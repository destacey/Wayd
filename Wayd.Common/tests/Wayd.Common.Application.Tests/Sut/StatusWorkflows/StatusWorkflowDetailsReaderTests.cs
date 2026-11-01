using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.Tests.Sut.StatusWorkflows.Conditions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// A workflow's detail: its transitions, and whether they stop it being published.
/// </summary>
public sealed class StatusWorkflowDetailsReaderTests : StatusWorkflowHandlerTestBase
{
    private StatusWorkflowDetailsReader CreateSut() => new(DbContext);

    /// <summary>Reads <paramref name="workflow"/> with only <paramref name="evaluators"/> able to check conditions.</summary>
    private Task<StatusWorkflowDetailsDto?> Read(StatusWorkflow workflow, params ITransitionConditionEvaluator[] evaluators) =>
        CreateSut().Read(w => w.Id == workflow.Id, new TransitionConditionChecker(evaluators).IsAvailable, TestContext.Current.CancellationToken);

    private static Guid StatusId(StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name).Id;

    // An owner type that declares its moves. Shipping accepts a reason and an approval, though nothing
    // here evaluates approvals.
    private static readonly WorkflowOwnerDescriptor Gadget = new(
        "test.query-gadget",
        "Gadget",
        new Dictionary<int, WorkflowAlias> { [21] = new("Ready", StatusCategory.Active), [22] = new("Shipped", StatusCategory.Done) },
        [21, 22],
        supportsRestrictedTransitions: true,
        moves:
        [
            WorkflowMove.Required("Prepare", fromAlias: null, 21),
            WorkflowMove.Optional("Ship", 21, 22, TransitionConditionKind.RequiresReason, TransitionConditionKind.RequiresPassedApproval),
        ]);

    // An owner type that requires every category, as well as Widget's aliases.
    private static readonly WorkflowOwnerDescriptor Staged = new(
        "test.query-staged",
        "Staged",
        new Dictionary<int, WorkflowAlias>
        {
            [NotableAlias] = new("Notable", StatusCategory.Active),
            [TerminalAlias] = new("Terminal", StatusCategory.Done),
        },
        [NotableAlias, TerminalAlias],
        [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed]);

    // An owner type that calls two of the categories by its own words.
    private static readonly WorkflowOwnerDescriptor Labelled = new(
        "test.query-labelled",
        "Labelled",
        new Dictionary<int, WorkflowAlias>
        {
            [NotableAlias] = new("Notable", StatusCategory.Active),
            [TerminalAlias] = new("Terminal", StatusCategory.Done),
        },
        [NotableAlias, TerminalAlias],
        categoryLabels: new Dictionary<StatusCategory, string>
        {
            [StatusCategory.Proposed] = "Planned",
            [StatusCategory.Done] = "Shipped",
        });

    public StatusWorkflowDetailsReaderTests() => WorkflowOwners.Register(Gadget, Staged, Labelled);

    [Fact]
    public async Task Handle_ShouldNameEachStatusCategory_AsTheOwnerTypeLabelsIt()
    {
        // Arrange
        var workflow = SeedWorkflow(owner: Labelled);

        // Act
        var result = await Read(workflow);

        // Assert
        result!.Statuses.Select(s => (s.Name, s.Category.Id, s.Category.Name)).Should().Equal(
            ("Proposed", (int)StatusCategory.Proposed, "Planned"),
            ("Notable", (int)StatusCategory.Active, "Active"),
            ("Terminal", (int)StatusCategory.Done, "Shipped"));
    }

    [Fact]
    public async Task Handle_ShouldNotOfferPublish_WhenARequiredCategoryIsMissing()
    {
        // Arrange
        var workflow = SeedWorkflow(owner: Staged);

        // Act
        var result = await Read(workflow);

        // Assert
        result!.MissingRequiredCategories.Should().Equal(StatusCategory.Removed);
        result.MissingRequiredAliases.Should().BeEmpty();
        result.CanPublish.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNameEachMisfiledAlias()
    {
        // Arrange
        var workflow = StatusWorkflow.CreateSystem("Seeded", null, Widget.Key, EventActor.System, Now).Value;
        workflow.AddSystemStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddSystemStatus("Notable", null, StatusCategory.Done, NotableAlias, EventActor.System, Now);
        DbContext.AddStatusWorkflow(workflow);

        // Act
        var result = await Read(workflow);

        // Assert
        result!.MisfiledAliases.Should().Equal(
            "'Notable' means Notable, which belongs under Active, not Done. Move it to Active or clear its meaning.");
        result.MissingRequiredCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldDescribeTheDeclaredMoves_WithTheConditionsSomethingCanCheck()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Gadget Workflow", null, Gadget.Key, EventActor.System, Now).Value;
        workflow.AddStatus("Planned", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddStatus("Ready", null, StatusCategory.Active, 21, EventActor.System, Now);
        workflow.AddStatus("Shipped", null, StatusCategory.Done, 22, EventActor.System, Now);
        DbContext.AddStatusWorkflow(workflow);
        var (planned, ready, shipped) = (StatusId(workflow, "Planned"), StatusId(workflow, "Ready"), StatusId(workflow, "Shipped"));

        // Act
        var result = await Read(workflow);

        // Assert
        result!.DeclaredMoves.Select(m => (m.FromStatusId, m.ToStatusId, m.IsRequired, string.Join(",", m.Actions)))
            .Should().Equal((planned, ready, true, "Prepare"), (ready, shipped, false, "Ship"));
        result.DeclaredMoves.Last().AcceptedConditions.Should().Equal(TransitionConditionKind.RequiresReason);
        result.DeclaredMoves.First().AcceptedConditions.Should().BeEmpty();
        result.AvailableConditions.Should().BeEmpty("no action makes a move outside the declared ones");
    }

    [Fact]
    public async Task Handle_ShouldDescribeAnAnyToAnyWorkflow()
    {
        // Arrange
        var workflow = SeedWorkflow();

        // Act
        var result = await Read(workflow);

        // Assert
        result.Should().NotBeNull();
        result!.TransitionMode.Should().Be(TransitionMode.AnyToAny);
        result.Transitions.Should().BeEmpty();
        result.TransitionIssues.Should().BeEmpty();
        result.SupportsRestrictedTransitions.Should().BeTrue();
        result.DeclaredMoves.Should().BeEmpty();
        result.CanPublish.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldListTransitionsInTheOrderOfTheirStatuses()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var (proposed, notable, terminal) = (StatusId(workflow, "Proposed"), StatusId(workflow, "Notable"), StatusId(workflow, "Terminal"));
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
        workflow.SetTransitions([new(notable, terminal), new(proposed, terminal), new(proposed, notable)], EventActor.System, Now);

        // Act
        var result = await Read(workflow);

        // Assert
        result!.TransitionMode.Should().Be(TransitionMode.Restricted);
        result.Transitions.Select(t => (t.FromStatusId, t.ToStatusId)).Should().Equal((proposed, notable), (proposed, terminal), (notable, terminal));
        result.CanPublish.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldCarryEachTransitionsConditions()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var (proposed, notable, terminal) = (StatusId(workflow, "Proposed"), StatusId(workflow, "Notable"), StatusId(workflow, "Terminal"));
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
        workflow.SetTransitions(
            [new(proposed, notable), new(notable, terminal, [TransitionConditionKind.RequiresReason])], EventActor.System, Now);

        // Act
        var result = await Read(workflow);

        // Assert
        result!.Transitions.Single(t => t.FromStatusId == notable).Conditions.Should().Equal(TransitionConditionKind.RequiresReason);
        result.Transitions.Single(t => t.FromStatusId == proposed).Conditions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOfferOnlyTheConditionsSomethingCanCheck()
    {
        // Arrange
        var workflow = SeedWorkflow();
        var approvals = new FakeConditionEvaluator(TransitionConditionKind.RequiresPassedApproval, isMet: true);

        // Act
        var without = await Read(workflow);
        var with = await Read(workflow, approvals);

        // Assert
        without!.AvailableConditions.Should().Equal(TransitionConditionKind.RequiresReason);
        with!.AvailableConditions.Should().Equal(TransitionConditionKind.RequiresReason, TransitionConditionKind.RequiresPassedApproval);
    }

    [Fact]
    public async Task Handle_ShouldNotOfferPublish_WhenTheTransitionsWouldStrandAStatus()
    {
        // Arrange
        var workflow = SeedWorkflow();
        workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, Now);
        workflow.SetTransitions([new(StatusId(workflow, "Proposed"), StatusId(workflow, "Notable"))], EventActor.System, Now);

        // Act
        var result = await Read(workflow);

        // Assert
        result!.TransitionIssues.Should().Equal(
            "'Notable' has no transition out of it, so a record that reaches it could not move on.",
            "'Terminal' cannot be reached from 'Proposed', the initial status.");
        result.MissingRequiredAliases.Should().BeEmpty();
        result.CanPublish.Should().BeFalse();
    }
}
