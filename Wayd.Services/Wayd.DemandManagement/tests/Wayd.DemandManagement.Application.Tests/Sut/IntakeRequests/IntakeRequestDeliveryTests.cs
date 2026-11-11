using FluentAssertions;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests;

public sealed class IntakeRequestDeliveryTests
{
    private static readonly EmployeeNavigationDto Agent = new() { Id = Guid.NewGuid(), Key = 1, Name = "Ada Agent" };

    [Fact]
    public void WithWorkItems_GivesEachFoundWorkItemOutcomeItsWorkItemAndSignal()
    {
        // Arrange
        var found = Outcome(IntakeRequestOutcomeKind.WorkItem);
        var gone = Outcome(IntakeRequestOutcomeKind.WorkItem);
        var project = Outcome(IntakeRequestOutcomeKind.NewProject) with { DeliverySignal = DeliverySignal.NotStarted };
        var workItem = WorkItem(found.TargetId, StatusCategory.Done);

        // Act
        var outcomes = IntakeRequestDelivery.WithWorkItems([found, gone, project], new Dictionary<Guid, IntakeRequestOutcomeWorkItemDto> { [found.TargetId] = workItem });

        // Assert
        outcomes[0].WorkItem.Should().Be(workItem);
        outcomes[0].DeliverySignal.Should().Be(DeliverySignal.Done);
        outcomes[1].WorkItem.Should().BeNull();
        outcomes[1].DeliverySignal.Should().BeNull("Work no longer has it");
        outcomes[2].Should().Be(project);
    }

    [Fact]
    public void WithWorkItems_NeverFillsAProjectOutcomeThatSharesTheWorkItemsId()
    {
        // Arrange
        var project = Outcome(IntakeRequestOutcomeKind.NewProject) with { DeliverySignal = DeliverySignal.NotStarted };

        // Act
        var outcomes = IntakeRequestDelivery.WithWorkItems([project], new Dictionary<Guid, IntakeRequestOutcomeWorkItemDto> { [project.TargetId] = WorkItem(project.TargetId, StatusCategory.Done) });

        // Assert
        outcomes.Single().Should().Be(project);
    }

    [Fact]
    public void WithWorkItems_OnARequestsOutcomes_RollsTheStateUpFromTheFilledSignals()
    {
        // Arrange — before Work's answer only the project reports, so the state reads done
        var workItemOutcome = Outcome(IntakeRequestOutcomeKind.WorkItem);
        var request = new IntakeRequestOutcomesDto
        {
            RequestId = Guid.NewGuid(),
            Outcomes = [Outcome(IntakeRequestOutcomeKind.NewProject) with { DeliverySignal = DeliverySignal.Done }, workItemOutcome],
            DeliveryState = DeliverySignal.Done,
        };

        // Act
        var filled = request.WithWorkItems(new Dictionary<Guid, IntakeRequestOutcomeWorkItemDto>
        {
            [workItemOutcome.TargetId] = WorkItem(workItemOutcome.TargetId, StatusCategory.Active),
        });

        // Assert
        filled.DeliveryState.Should().Be(DeliverySignal.InProgress);
        filled.Outcomes[1].DeliverySignal.Should().Be(DeliverySignal.InProgress);
    }

    [Fact]
    public void StateOf_IgnoresOutcomesThatReportNoSignal()
    {
        // Arrange
        IntakeRequestOutcomeDto[] outcomes =
        [
            Outcome(IntakeRequestOutcomeKind.WorkItem),
            Outcome(IntakeRequestOutcomeKind.NewProject) with { DeliverySignal = DeliverySignal.Cancelled },
        ];

        // Act
        var state = IntakeRequestDelivery.StateOf(outcomes);

        // Assert
        state.Should().Be(DeliverySignal.Cancelled);
    }

    [Fact]
    public void StateOf_WhenNoOutcomeReportsASignal_IsNull()
    {
        // Arrange
        IntakeRequestOutcomeDto[] outcomes = [Outcome(IntakeRequestOutcomeKind.WorkItem)];

        // Act
        var state = IntakeRequestDelivery.StateOf(outcomes);

        // Assert
        state.Should().BeNull();
    }

    private static IntakeRequestOutcomeDto Outcome(IntakeRequestOutcomeKind kind) => new()
    {
        Kind = kind,
        TargetId = Guid.NewGuid(),
        AddedBy = Agent,
    };

    private static IntakeRequestOutcomeWorkItemDto WorkItem(Guid id, StatusCategory category) => new()
    {
        Id = id,
        Key = "WAYD-1",
        Title = "Export the ledger",
        WorkspaceKey = "WAYD",
        Status = category.ToString(),
        StatusCategory = category,
    };
}
