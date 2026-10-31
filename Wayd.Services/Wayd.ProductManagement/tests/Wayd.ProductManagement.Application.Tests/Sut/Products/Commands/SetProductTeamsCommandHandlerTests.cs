using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class SetProductTeamsCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IDispatcher> _dispatcher = new();

    private SetProductTeamsCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, _dispatcher.Object, Logger<SetProductTeamsCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldLinkTheNewTeams_UnlinkTheOmittedOnes_AndLeaveTheRest()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var kept = SeedTeam("Payments", "PAY");
        var dropped = SeedTeam("Legacy Ops", "LEG");
        var added = SeedTeam("Atlas", "ATL");
        SeedTeamLink(web, kept);
        SeedTeamLink(web, dropped);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetProductTeamsCommand(web.Id, [kept.Id, added.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.TeamLinks.Select(l => l.TeamId).Should().BeEquivalentTo([kept.Id, added.Id]);
        web.DomainEvents.OfType<ProductTeamUnlinkedEvent>().Should().ContainSingle().Which.TeamId.Should().Be(dropped.Id);
        web.DomainEvents.OfType<ProductTeamLinkedEvent>().Should().ContainSingle().Which.TeamId.Should().Be(added.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheLinksOfProductsBeneathAlone()
    {
        // Arrange
        var storefront = SeedProduct("Storefront");
        var web = SeedProduct("Storefront Web", storefront.Id);
        var payments = SeedTeam();
        SeedTeamLink(web, payments);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetProductTeamsCommand(storefront.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.TeamLinks.Should().ContainSingle();
        storefront.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenATeamHasNoCopyYet_ShouldCopyItFromOrganizationAndLinkIt()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var source = new TeamSource(Guid.CreateVersion7(), 42, "Payments", new TeamCode("PAY"), TeamType.Team, true);
        _dispatcher
            .Setup(d => d.Send(It.Is<GetSimpleTeamQuery>(q => q.Id == source.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetProductTeamsCommand(web.Id, [source.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.ProductTeams.Should().ContainSingle().Which.Id.Should().Be(source.Id);
        web.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(source.Id);
    }

    [Fact]
    public async Task Handle_WhenATeamIsRefused_ShouldChangeNothingAndNameIt()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var dropped = SeedTeam("Payments", "PAY");
        var atlas = SeedTeam("Atlas", "ATL");
        var retired = SeedTeam("Legacy Ops", "LEG", isActive: false);
        SeedTeamLink(web, dropped);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetProductTeamsCommand(web.Id, [atlas.Id, retired.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Legacy Ops: An inactive team cannot be linked to a product.");
        web.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldKeepAnInactiveTeamAlreadyLinked()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var team = SeedTeam();
        SeedTeamLink(web, team);
        typeof(ProductTeam).GetProperty(nameof(ProductTeam.IsActive))!.SetValue(team, false);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetProductTeamsCommand(web.Id, [team.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.TeamLinks.Should().ContainSingle();
        web.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetProductTeamsCommand(Guid.CreateVersion7(), []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
