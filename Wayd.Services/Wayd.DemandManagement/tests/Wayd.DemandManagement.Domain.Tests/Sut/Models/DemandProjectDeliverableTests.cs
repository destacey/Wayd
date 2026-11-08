using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class DemandProjectDeliverableTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void Constructor_CopiesTheSourceAndStampsEveryGroup()
    {
        // Arrange
        var source = SourceProjectDeliverable.Accepted(Guid.CreateVersion7(), "Ledger");

        // Act
        var sut = new DemandProjectDeliverable(source, Created);

        // Assert
        sut.Id.Should().Be(source.Id);
        sut.ProjectId.Should().Be(source.ProjectId);
        sut.Name.Should().Be("Ledger");
        sut.StatusCategory.Should().Be(StatusCategory.Done);
        sut.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        sut.IsAccepted.Should().BeTrue();
        sut.Watermarks.Should().Be(DemandProjectDeliverableWatermarks.At(Created));
    }

    [Theory]
    [InlineData(ProjectDeliverableStatusAlias.NotStarted, false)]
    [InlineData(ProjectDeliverableStatusAlias.InProgress, false)]
    [InlineData(ProjectDeliverableStatusAlias.Descoped, false)]
    [InlineData(ProjectDeliverableStatusAlias.None, false)]
    [InlineData(ProjectDeliverableStatusAlias.Accepted, true)]
    public void IsAccepted_IsTrueOnlyForTheAcceptedAlias(ProjectDeliverableStatusAlias alias, bool expected)
    {
        // Arrange
        var source = SourceProjectDeliverable.Any() with { StatusAlias = alias };

        // Act
        var sut = new DemandProjectDeliverable(source, Created);

        // Assert
        sut.IsAccepted.Should().Be(expected);
    }

    [Fact]
    public void ApplyName_WhenNewer_RenamesWithoutTouchingTheStatusWatermark()
    {
        // Arrange
        var sut = new DemandProjectDeliverable(SourceProjectDeliverable.Any(name: "Ledger"), Created);

        // Act
        var applied = sut.ApplyName("General Ledger", Later);

        // Assert
        applied.Should().BeTrue();
        sut.Name.Should().Be("General Ledger");
        sut.Watermarks.Should().Be(new DemandProjectDeliverableWatermarks(Later, Created));
    }

    [Fact]
    public void ApplyName_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var sut = new DemandProjectDeliverable(SourceProjectDeliverable.Any(name: "Ledger"), Created);

        // Act
        var applied = sut.ApplyName("General Ledger", Earlier);

        // Assert
        applied.Should().BeFalse();
        sut.Name.Should().Be("Ledger");
    }

    [Fact]
    public void ApplyName_WhenRedelivered_ReportsNothingToApply()
    {
        // Arrange
        var sut = new DemandProjectDeliverable(SourceProjectDeliverable.Any(name: "Ledger"), Created);
        sut.ApplyName("General Ledger", Later);

        // Act
        var applied = sut.ApplyName("General Ledger", Later);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void ApplyStatus_WhenNewer_MovesBothCategoryAndAlias()
    {
        // Arrange
        var sut = new DemandProjectDeliverable(SourceProjectDeliverable.Any(), Created);

        // Act
        var applied = sut.ApplyStatus(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Later);

        // Assert
        applied.Should().BeTrue();
        sut.StatusCategory.Should().Be(StatusCategory.Done);
        sut.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        sut.Watermarks.Should().Be(new DemandProjectDeliverableWatermarks(Created, Later));
    }

    [Fact]
    public void ApplyStatus_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange — accepted at Later, then a start from before it arrives.
        var sut = new DemandProjectDeliverable(SourceProjectDeliverable.Any(), Created);
        sut.ApplyStatus(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Later);

        // Act
        var applied = sut.ApplyStatus(StatusCategory.Active, ProjectDeliverableStatusAlias.InProgress, Created);

        // Assert
        applied.Should().BeFalse();
        sut.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }

    [Fact]
    public void ApplyStatus_WhenRedelivered_ReportsNothingToApply()
    {
        // Arrange
        var sut = new DemandProjectDeliverable(SourceProjectDeliverable.Any(), Created);
        sut.ApplyStatus(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Later);

        // Act
        var applied = sut.ApplyStatus(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Later);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void Resync_WhenTheSourceDiffers_OverwritesAndStampsOnlyTheGroupsThatChanged()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(name: "Ledger");
        var sut = new DemandProjectDeliverable(source, Created);

        // Act
        var changed = sut.Resync(source with { StatusCategory = StatusCategory.Done, StatusAlias = ProjectDeliverableStatusAlias.Accepted }, Later);

        // Assert
        changed.Should().BeTrue();
        sut.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        sut.Watermarks.Should().Be(new DemandProjectDeliverableWatermarks(Created, Later));
    }

    [Fact]
    public void Resync_WhenTheSourceMatches_KeepsTheWatermarks()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any();
        var sut = new DemandProjectDeliverable(source, Created);

        // Act
        var changed = sut.Resync(source, Later);

        // Assert
        changed.Should().BeFalse();
        sut.Watermarks.Should().Be(DemandProjectDeliverableWatermarks.At(Created));
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsAChangeNewerThanTheRead_LeavesThatGroupAlone()
    {
        // Arrange — the copy took an acceptance after the read began.
        var source = SourceProjectDeliverable.Any();
        var sut = new DemandProjectDeliverable(source, Created);
        sut.ApplyStatus(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Later);

        // Act
        var changed = sut.Resync(source, Created);

        // Assert
        changed.Should().BeFalse();
        sut.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }

    [Fact]
    public void Resync_WithAnotherDeliverable_Throws()
    {
        // Arrange
        var sut = new DemandProjectDeliverable(SourceProjectDeliverable.Any(), Created);

        // Act
        var act = () => sut.Resync(SourceProjectDeliverable.Any(), Later);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
