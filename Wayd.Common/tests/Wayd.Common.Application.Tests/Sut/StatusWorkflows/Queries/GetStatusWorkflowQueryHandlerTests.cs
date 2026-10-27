using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>
/// One workflow, as workflow administration reads it.
/// </summary>
public sealed class GetStatusWorkflowQueryHandlerTests : StatusWorkflowHandlerTestBase
{
    private readonly Mock<IStatusWorkflowDetailsReader> _detailsReader = new();

    private GetStatusWorkflowQueryHandler CreateSut() =>
        new(_detailsReader.Object, Mock.Of<ITransitionConditionChecker>(), OwnerAvailability);

    private void ReaderReturns(WorkflowOwnerDescriptor owner) =>
        _detailsReader
            .Setup(r => r.Read(
                It.IsAny<Expression<Func<StatusWorkflow, bool>>>(),
                It.IsAny<Func<TransitionConditionKind, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatusWorkflowDetailsDto
            {
                Id = Guid.CreateVersion7(),
                Key = 1,
                Name = $"{owner.DisplayName} Workflow",
                Owner = new WorkflowOwnerNavigationDto { Key = owner.Key, Name = owner.DisplayName },
                State = nameof(StatusWorkflowState.Published),
                IsSystem = false,
                IsAssigned = false,
                Statuses = [],
                TransitionMode = TransitionMode.AnyToAny,
                Transitions = [],
                AvailableConditions = [],
                DeclaredMoves = [],
                SupportsRestrictedTransitions = false,
                TransitionIssues = [],
                MissingRequiredAliases = [],
                MissingRequiredCategories = [],
                MisfiledAliases = [],
                CanEdit = false,
                CanPublish = false,
                CanArchive = false,
            });

    [Fact]
    public async Task Handle_ShouldReadAsNotFound_WhenTheWorkflowsFeatureIsOff()
    {
        // Arrange
        ReaderReturns(Gadget);

        // Act
        var workflow = await CreateSut().Handle(new GetStatusWorkflowQuery(new IdOrKey(1)), TestContext.Current.CancellationToken);

        // Assert
        workflow.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnTheWorkflow_OnceItsFeatureIsOn()
    {
        // Arrange
        EnableGadgets();
        ReaderReturns(Gadget);

        // Act
        var workflow = await CreateSut().Handle(new GetStatusWorkflowQuery(new IdOrKey(1)), TestContext.Current.CancellationToken);

        // Assert
        workflow.Should().NotBeNull();
    }
}
