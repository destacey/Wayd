using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class WorkflowMoveTests
{
    [Fact]
    public void Required_ShouldDeclareARequiredMove_WithItsConditions()
    {
        // Act
        var move = WorkflowMove.Required(" Ship ", 1, 2, TransitionConditionKind.RequiresReason);

        // Assert
        move.Action.Should().Be("Ship");
        move.FromAlias.Should().Be(1);
        move.ToAlias.Should().Be(2);
        move.IsRequired.Should().BeTrue();
        move.AcceptedConditions.Should().BeEquivalentTo([TransitionConditionKind.RequiresReason]);
    }

    [Fact]
    public void Optional_ShouldDeclareAnOptionalMove_FromTheInitialStatus_AcceptingNoConditions()
    {
        // Act
        var move = WorkflowMove.Optional("Ship", fromAlias: null, 2);

        // Assert
        move.FromAlias.Should().BeNull();
        move.IsRequired.Should().BeFalse();
        move.AcceptedConditions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StatusWorkflow.NoAlias, 2)]
    [InlineData(1, StatusWorkflow.NoAlias)]
    public void Required_ShouldReject_NoAliasAsAnEnd(int fromAlias, int toAlias)
    {
        // Act
        Action act = () => WorkflowMove.Required("Ship", fromAlias, toAlias);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*NoAlias*");
    }

    [Fact]
    public void Optional_ShouldDeclareAMoveBackToTheInitialStatus()
    {
        // Act
        var move = WorkflowMove.Optional("Revert", 2, toAlias: null);

        // Assert
        move.FromAlias.Should().Be(2);
        move.ToAlias.Should().BeNull();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(null, null)]
    public void Required_ShouldReject_AMoveFromAStatusToItself(int? fromAlias, int? toAlias)
    {
        // Act
        Action act = () => WorkflowMove.Required("Ship", fromAlias, toAlias);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*to itself*");
    }

    [Fact]
    public void Optional_ShouldReject_ARepeatedCondition()
    {
        // Act
        Action act = () => WorkflowMove.Optional("Ship", 1, 2, TransitionConditionKind.RequiresReason, TransitionConditionKind.RequiresReason);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*unknown or repeated condition*");
    }
}
