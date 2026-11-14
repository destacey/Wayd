using FluentAssertions;
using Wayd.ProductManagement.Application.Incidents;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents;

public sealed class IncidentTeamsMembershipTests
{
    [Fact]
    public void ShouldInvite_IsFalse_UntilTheConnectorCanAddMembers()
    {
        // Arrange
        var canAddMembers = IncidentTeamsMembership.ConnectorCanAddMembers;

        // Act
        var invite = IncidentTeamsMembership.ShouldInvite(
            createdByWayd: true, reportedByChanged: true, commanderChanged: false);

        // Assert
        canAddMembers.Should().BeFalse();
        invite.Should().BeFalse();
    }

    [Fact]
    public void SkippedBecauseMembersCannotBeAdded_OnlyWhenWaydCreatedTheChat_AndAWatchedPersonChanges()
    {
        // Arrange
        var createdAndReportedByChanged = (createdByWayd: true, reportedByChanged: true, commanderChanged: false);
        var notCreated = (createdByWayd: false, reportedByChanged: true, commanderChanged: true);
        var nothingChanged = (createdByWayd: true, reportedByChanged: false, commanderChanged: false);

        // Act
        var skipped = IncidentTeamsMembership.SkippedBecauseMembersCannotBeAdded(
            createdAndReportedByChanged.createdByWayd,
            createdAndReportedByChanged.reportedByChanged,
            createdAndReportedByChanged.commanderChanged);
        var notWaydsChat = IncidentTeamsMembership.SkippedBecauseMembersCannotBeAdded(
            notCreated.createdByWayd, notCreated.reportedByChanged, notCreated.commanderChanged);
        var unchanged = IncidentTeamsMembership.SkippedBecauseMembersCannotBeAdded(
            nothingChanged.createdByWayd, nothingChanged.reportedByChanged, nothingChanged.commanderChanged);

        // Assert
        skipped.Should().BeTrue();
        notWaydsChat.Should().BeFalse();
        unchanged.Should().BeFalse();
    }
}
