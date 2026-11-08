using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeRequestOutcomeTests
{
    private static readonly Instant At = Instant.FromUtc(2026, 1, 15, 9, 0, 0);

    [Fact]
    public void IsMet_WithNothingMapped_IsUnknown()
    {
        // Arrange

        // Act
        var met = IntakeRequestOutcome.IsMet([]);

        // Assert
        met.Should().BeNull();
    }

    [Fact]
    public void IsMet_WhenEveryMappedDeliverableIsAccepted_IsTrue()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        DemandProjectDeliverable[] mapped =
        [
            new(SourceProjectDeliverable.Accepted(projectId, "Portal"), At),
            new(SourceProjectDeliverable.Accepted(projectId, "Reports"), At),
        ];

        // Act
        var met = IntakeRequestOutcome.IsMet(mapped);

        // Assert
        met.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectDeliverableStatusAlias.NotStarted)]
    [InlineData(ProjectDeliverableStatusAlias.InProgress)]
    [InlineData(ProjectDeliverableStatusAlias.Descoped)]
    [InlineData(ProjectDeliverableStatusAlias.None)]
    public void IsMet_WhileAnyMappedDeliverableIsNotAccepted_IsFalse(ProjectDeliverableStatusAlias other)
    {
        // Arrange
        var projectId = Guid.NewGuid();
        DemandProjectDeliverable[] mapped =
        [
            new(SourceProjectDeliverable.Accepted(projectId, "Portal"), At),
            new(SourceProjectDeliverable.Any(projectId, "Reports") with { StatusAlias = other }, At),
        ];

        // Act
        var met = IntakeRequestOutcome.IsMet(mapped);

        // Assert
        met.Should().BeFalse();
    }
}
