using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public sealed class WorkTeamTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void Constructor_StampsEveryGroupWithTheCreatingChange()
    {
        // Arrange
        var source = new WorkTeamFaker(TeamType.Team).Generate();

        // Act
        var team = new WorkTeam(source, Created);

        // Assert
        team.Watermarks.Should().Be(new TeamReplicaWatermarks(Created, Created));
    }

    [Fact]
    public void ApplyDetails_WhenNewerThanTheLastChange_AppliesAndAdvancesTheWatermark()
    {
        // Arrange
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithName("Atlas").Generate(), Created);

        // Act
        var applied = team.ApplyDetails("Borealis", new TeamCode("BOR"), Later);

        // Assert
        applied.Should().BeTrue();
        team.Name.Should().Be("Borealis");
        team.Code.Value.Should().Be("BOR");
        team.Watermarks.Details.Should().Be(Later);
    }

    [Fact]
    public void ApplyDetails_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange — a rename delivered after a newer one already applied.
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithName("Atlas").WithCode("ATL").Generate(), Created);

        // Act
        var applied = team.ApplyDetails("Borealis", new TeamCode("BOR"), Earlier);

        // Assert
        applied.Should().BeFalse();
        team.Name.Should().Be("Atlas");
        team.Watermarks.Details.Should().Be(Created);
    }

    [Fact]
    public void ApplyDetails_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).Generate(), Created);
        team.ApplyDetails("Borealis", new TeamCode("BOR"), Later);

        // Act
        var applied = team.ApplyDetails("Borealis", new TeamCode("BOR"), Later);

        // Assert
        applied.Should().BeFalse();
        team.Name.Should().Be("Borealis");
    }

    [Fact]
    public void ApplyDetails_WhenANewerChangeOnlyTouchedActivation_StillApplies()
    {
        // Arrange — the groups order independently: a deactivation at Later says nothing about the name.
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithName("Atlas").Generate(), Earlier);
        team.ApplyActivation(false, Later);

        // Act
        var applied = team.ApplyDetails("Borealis", new TeamCode("BOR"), Created);

        // Assert
        applied.Should().BeTrue();
        team.Name.Should().Be("Borealis");
        team.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ApplyActivation_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange — reactivated at Later, then the earlier deactivation arrives.
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithIsActive(false).Generate(), Created);
        team.ApplyActivation(true, Later);

        // Act
        var applied = team.ApplyActivation(false, Created);

        // Assert
        applied.Should().BeFalse();
        team.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ApplyActivation_WhenTheValueIsUnchangedButNewer_AdvancesTheWatermark()
    {
        // Arrange — deactivated then reactivated, delivered as the reactivation only so far.
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithIsActive(true).Generate(), Created);

        // Act
        var applied = team.ApplyActivation(true, Later);

        // Assert
        applied.Should().BeTrue();
        team.Watermarks.Activation.Should().Be(Later);
        team.ApplyActivation(false, Created).Should().BeFalse("the deactivation predates the reactivation");
    }

    [Fact]
    public void Resync_WhenAGroupAlreadyMatches_KeepsItsWatermark()
    {
        // Arrange
        var source = new WorkTeamFaker(TeamType.Team).Generate();
        var team = new WorkTeam(source, Created);

        // Act
        var changed = team.Resync(source, Later);

        // Assert — stamping Later would skip a change timestamped before the read but committed after it.
        changed.Should().BeFalse();
        team.Watermarks.Should().Be(new TeamReplicaWatermarks(Created, Created));
    }

    [Fact]
    public void Resync_WhenAGroupDiffers_OverwritesOnlyThatGroupAndStampsTheRead()
    {
        // Arrange
        var id = Guid.NewGuid();
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Atlas").WithIsActive(true).Generate(), Created);
        var source = new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Borealis").WithCode(team.Code).WithIsActive(true).Generate();

        // Act
        var changed = team.Resync(source, Later);

        // Assert
        changed.Should().BeTrue();
        team.Name.Should().Be("Borealis");
        team.Watermarks.Should().Be(new TeamReplicaWatermarks(Later, Created));
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsAChangeNewerThanTheRead_LeavesThatGroupAlone()
    {
        // Arrange — an event applied after the sync read the source.
        var id = Guid.NewGuid();
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Atlas").Generate(), Earlier);
        team.ApplyDetails("Borealis", new TeamCode("BOR"), Later);
        var readBeforeTheRename = new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Atlas").WithIsActive(team.IsActive).Generate();

        // Act
        var changed = team.Resync(readBeforeTheRename, Created);

        // Assert
        changed.Should().BeFalse();
        team.Name.Should().Be("Borealis");
    }

    [Fact]
    public void Resync_WithADifferentTeam_Throws()
    {
        // Arrange
        var team = new WorkTeam(new WorkTeamFaker(TeamType.Team).Generate(), Created);
        var other = new WorkTeamFaker(TeamType.Team).Generate();

        // Act
        var act = () => team.Resync(other, Later);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
