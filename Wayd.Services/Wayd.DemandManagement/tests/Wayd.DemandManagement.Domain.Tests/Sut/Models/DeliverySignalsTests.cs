using FluentAssertions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class DeliverySignalsTests
{
    [Theory]
    [InlineData(LifecycleCategory.NotStarted, DeliverySignal.NotStarted)]
    [InlineData(LifecycleCategory.Active, DeliverySignal.InProgress)]
    [InlineData(LifecycleCategory.Completed, DeliverySignal.Done)]
    [InlineData(LifecycleCategory.Canceled, DeliverySignal.Cancelled)]
    public void Of_ALifecycleCategory_MapsToItsSignal(LifecycleCategory category, DeliverySignal expected)
    {
        // Arrange — the category is the input.

        // Act
        var signal = DeliverySignals.Of(category);

        // Assert
        signal.Should().Be(expected);
    }

    [Theory]
    [InlineData(StatusCategory.Proposed, DeliverySignal.NotStarted)]
    [InlineData(StatusCategory.Active, DeliverySignal.InProgress)]
    [InlineData(StatusCategory.Done, DeliverySignal.Done)]
    [InlineData(StatusCategory.Removed, DeliverySignal.Cancelled)]
    public void Of_AStatusCategory_MapsToItsSignal(StatusCategory category, DeliverySignal expected)
    {
        // Arrange — the category is the input.

        // Act
        var signal = DeliverySignals.Of(category);

        // Assert
        signal.Should().Be(expected);
    }

    [Theory]
    [InlineData(ProjectDeliverableStatusAlias.NotStarted, StatusCategory.Active, DeliverySignal.NotStarted)]
    [InlineData(ProjectDeliverableStatusAlias.InProgress, StatusCategory.Proposed, DeliverySignal.InProgress)]
    [InlineData(ProjectDeliverableStatusAlias.Accepted, StatusCategory.Active, DeliverySignal.Done)]
    [InlineData(ProjectDeliverableStatusAlias.Descoped, StatusCategory.Done, DeliverySignal.Cancelled)]
    public void Of_ADeliverableWithAWellKnownStatus_MapsByItsAliasNotItsCategory(ProjectDeliverableStatusAlias alias, StatusCategory category, DeliverySignal expected)
    {
        // Arrange — each category disagrees with its alias, so only the alias can produce the expected signal.

        // Act
        var signal = DeliverySignals.Of(alias, category);

        // Assert
        signal.Should().Be(expected);
    }

    [Theory]
    [InlineData(StatusCategory.Proposed, DeliverySignal.NotStarted)]
    [InlineData(StatusCategory.Active, DeliverySignal.InProgress)]
    [InlineData(StatusCategory.Done, DeliverySignal.Done)]
    [InlineData(StatusCategory.Removed, DeliverySignal.Cancelled)]
    public void Of_ADeliverableInACompanysOwnStatus_MapsByItsCategory(StatusCategory category, DeliverySignal expected)
    {
        // Arrange — a status a company added has no alias.

        // Act
        var signal = DeliverySignals.Of(ProjectDeliverableStatusAlias.None, category);

        // Assert
        signal.Should().Be(expected);
    }

    [Fact]
    public void Rollup_OfNothing_IsNull()
    {
        // Arrange — no signals.

        // Act
        var state = DeliverySignals.Rollup([]);

        // Assert
        state.Should().BeNull();
    }

    [Theory]
    [InlineData(DeliverySignal.Blocked, new[] { DeliverySignal.Done, DeliverySignal.Blocked, DeliverySignal.InProgress, DeliverySignal.NotStarted, DeliverySignal.Cancelled })]
    [InlineData(DeliverySignal.InProgress, new[] { DeliverySignal.Done, DeliverySignal.NotStarted, DeliverySignal.InProgress, DeliverySignal.Cancelled })]
    [InlineData(DeliverySignal.NotStarted, new[] { DeliverySignal.Done, DeliverySignal.Cancelled, DeliverySignal.NotStarted })]
    [InlineData(DeliverySignal.Done, new[] { DeliverySignal.Cancelled, DeliverySignal.Done, DeliverySignal.Cancelled })]
    [InlineData(DeliverySignal.Cancelled, new[] { DeliverySignal.Cancelled, DeliverySignal.Cancelled })]
    public void Rollup_TakesTheFirstSignalPresentInPrecedenceOrder(DeliverySignal expected, DeliverySignal[] signals)
    {
        // Arrange — each set holds the expected signal and only signals that rank below it.

        // Act
        var state = DeliverySignals.Rollup(signals);

        // Assert
        state.Should().Be(expected);
    }
}
