using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class ProductTeamTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void ApplyDetails_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var team = new ProductTeam(new ProductTeamFaker().WithName("Atlas").Generate(), Created);

        // Act
        var applied = team.ApplyDetails("Borealis", new TeamCode("BOR"), Earlier);

        // Assert
        applied.Should().BeFalse();
        team.Name.Should().Be("Atlas");
    }

    [Fact]
    public void ApplyDetails_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var team = new ProductTeam(new ProductTeamFaker().Generate(), Created);
        team.ApplyDetails("Borealis", new TeamCode("BOR"), Later);

        // Act
        var applied = team.ApplyDetails("Borealis", new TeamCode("BOR"), Later);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void ApplyActivation_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange — reactivated at Later, then the earlier deactivation arrives.
        var team = new ProductTeam(new ProductTeamFaker().WithIsActive(false).Generate(), Created);
        team.ApplyActivation(true, Later);

        // Act
        var applied = team.ApplyActivation(false, Created);

        // Assert
        applied.Should().BeFalse();
        team.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ApplyActivation_WhenANewerChangeOnlyTouchedDetails_StillApplies()
    {
        // Arrange
        var team = new ProductTeam(new ProductTeamFaker().WithIsActive(true).Generate(), Earlier);
        team.ApplyDetails("Borealis", new TeamCode("BOR"), Later);

        // Act
        var applied = team.ApplyActivation(false, Created);

        // Assert
        applied.Should().BeTrue();
        team.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Resync_WhenAGroupAlreadyMatches_KeepsItsWatermark()
    {
        // Arrange
        var source = new ProductTeamFaker().Generate();
        var team = new ProductTeam(source, Created);

        // Act
        var changed = team.Resync(source, Later);

        // Assert
        changed.Should().BeFalse();
        team.Watermarks.Should().Be(new TeamReplicaWatermarks(Created, Created));
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsAChangeNewerThanTheRead_LeavesThatGroupAlone()
    {
        // Arrange
        var id = Guid.NewGuid();
        var team = new ProductTeam(new ProductTeamFaker().WithId(id).WithIsActive(true).Generate(), Earlier);
        team.ApplyActivation(false, Later);
        var readBeforeTheDeactivation = new ProductTeamFaker().WithId(id).WithName(team.Name).WithCode(team.Code).WithIsActive(true).Generate();

        // Act
        var changed = team.Resync(readBeforeTheDeactivation, Created);

        // Assert
        changed.Should().BeFalse();
        team.IsActive.Should().BeFalse();
    }
}
