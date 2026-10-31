using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

/// <summary>
/// Linking teams to a product and unlinking them: any number of teams, each linked once.
/// </summary>
public sealed class ProductTeamLinkTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly ProductFaker _faker = new();

    private static ProductTeam Team(bool isActive = true) => new ProductTeamFaker().WithIsActive(isActive).Generate();

    #region LinkTeam

    [Fact]
    public void LinkTeam_ShouldAddTheLinkAndRaiseTheLinkedEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        var team = Team();

        // Act
        var result = sut.LinkTeam(team, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var link = sut.TeamLinks.Should().ContainSingle().Subject;
        link.ProductId.Should().Be(sut.Id);
        link.TeamId.Should().Be(team.Id);

        var linked = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTeamLinkedEvent>().Subject;
        linked.Id.Should().Be(sut.Id);
        linked.Key.Should().Be(sut.Key);
        linked.TeamId.Should().Be(team.Id);
        linked.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void LinkTeam_ShouldAllowManyTeams()
    {
        // Arrange
        var sut = _faker.Generate();
        ProductTeam[] teams = [Team(), Team(), Team()];

        // Act
        foreach (var team in teams)
        {
            sut.LinkTeam(team, EventActor.System, Now);
        }

        // Assert
        sut.TeamLinks.Select(l => l.TeamId).Should().BeEquivalentTo(teams.Select(t => t.Id));
        sut.DomainEvents.OfType<ProductTeamLinkedEvent>().Should().HaveCount(3);
    }

    [Fact]
    public void LinkTeam_ShouldSucceedWithoutChange_WhenTheTeamIsAlreadyLinked()
    {
        // Arrange
        var sut = _faker.Generate();
        var team = Team();
        sut.LinkTeam(team, EventActor.System, Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.LinkTeam(team, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.TeamLinks.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void LinkTeam_ShouldFail_WhenTheTeamIsInactive()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.LinkTeam(Team(isActive: false), EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An inactive team cannot be linked to a product.");
        sut.TeamLinks.Should().BeEmpty();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void LinkTeam_ShouldSucceedWithoutChange_WhenAnAlreadyLinkedTeamHasSinceBeenDeactivated()
    {
        // Arrange
        var sut = _faker.Generate();
        var team = Team();
        sut.LinkTeam(team, EventActor.System, Now);
        sut.ClearDomainEvents();
        var deactivated = new ProductTeamFaker().WithId(team.Id).WithIsActive(false).Generate();

        // Act
        var result = sut.LinkTeam(deactivated, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.TeamLinks.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion LinkTeam

    #region UnlinkTeam

    [Fact]
    public void UnlinkTeam_ShouldRemoveTheLinkAndRaiseTheUnlinkedEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        var team = Team();
        var other = Team();
        sut.LinkTeam(team, EventActor.System, Now);
        sut.LinkTeam(other, EventActor.System, Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.UnlinkTeam(team.Id, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(other.Id);

        var unlinked = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTeamUnlinkedEvent>().Subject;
        unlinked.Id.Should().Be(sut.Id);
        unlinked.Key.Should().Be(sut.Key);
        unlinked.TeamId.Should().Be(team.Id);
        unlinked.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void UnlinkTeam_ShouldSucceedWithoutChange_WhenTheTeamIsNotLinked()
    {
        // Arrange
        var sut = _faker.Generate();
        sut.LinkTeam(Team(), EventActor.System, Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.UnlinkTeam(Guid.NewGuid(), EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.TeamLinks.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion UnlinkTeam
}
