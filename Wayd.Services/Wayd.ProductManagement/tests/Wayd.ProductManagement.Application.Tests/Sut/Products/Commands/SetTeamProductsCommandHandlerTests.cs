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

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class SetTeamProductsCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IDispatcher> _dispatcher = new();

    private SetTeamProductsCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, _dispatcher.Object, Logger<SetTeamProductsCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldLinkTheNewProducts_UnlinkTheOmittedOnes_AndLeaveTheRest()
    {
        // Arrange
        var kept = SeedProduct("Checkout");
        var dropped = SeedProduct("Legacy Cart");
        var added = SeedProduct("Storefront Web");
        var untouched = SeedProduct("Identity Service");
        var payments = SeedTeam("Payments", "PAY");
        var atlas = SeedTeam("Atlas", "ATL");
        SeedTeamLink(kept, payments);
        SeedTeamLink(dropped, payments);
        SeedTeamLink(untouched, atlas);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetTeamProductsCommand(payments.Id, [kept.Id, added.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        kept.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(payments.Id);
        kept.DomainEvents.Should().BeEmpty();
        dropped.TeamLinks.Should().BeEmpty();
        dropped.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTeamUnlinkedEvent>();
        added.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(payments.Id);
        added.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTeamLinkedEvent>();
        untouched.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(atlas.Id);
        untouched.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoProducts_ShouldUnlinkTheTeamFromEverything()
    {
        // Arrange
        var checkout = SeedProduct("Checkout");
        var web = SeedProduct("Storefront Web");
        var payments = SeedTeam();
        SeedTeamLink(checkout, payments);
        SeedTeamLink(web, payments);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetTeamProductsCommand(payments.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        checkout.TeamLinks.Should().BeEmpty();
        web.TeamLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheTeamHasNoCopyYet_ShouldCopyItFromOrganizationAndLinkIt()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var source = new TeamSource(Guid.CreateVersion7(), 42, "Payments", new TeamCode("PAY"), TeamType.Team, true);
        _dispatcher
            .Setup(d => d.Send(It.Is<GetSimpleTeamQuery>(q => q.Id == source.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetTeamProductsCommand(source.Id, [web.Id]), TestContext.Current.CancellationToken);

        // Assert — the copy is saved on its own before the links
        result.IsSuccess.Should().BeTrue();
        DbContext.ProductTeams.Should().ContainSingle().Which.Id.Should().Be(source.Id);
        web.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(source.Id);
        DbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenTheTeamDoesNotExist_ShouldFailWithoutSaving()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleTeamQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ISimpleTeam?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetTeamProductsCommand(Guid.CreateVersion7(), [web.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Team not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAProductDoesNotExist_ShouldChangeNothing()
    {
        // Arrange
        var checkout = SeedProduct("Checkout");
        var payments = SeedTeam();
        SeedTeamLink(checkout, payments);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new SetTeamProductsCommand(payments.Id, [Guid.CreateVersion7()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
        checkout.TeamLinks.Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnInactiveTeam_ShouldRefuseNewProducts_ButKeepOrUnlinkCurrentOnes()
    {
        // Arrange
        var kept = SeedProduct("Checkout");
        var dropped = SeedProduct("Legacy Cart");
        var added = SeedProduct("Storefront Web");
        var team = SeedTeam(isActive: true);
        SeedTeamLink(kept, team);
        SeedTeamLink(dropped, team);
        typeof(Wayd.ProductManagement.Domain.Models.ProductTeam).GetProperty("IsActive")!.SetValue(team, false);
        var sut = CreateSut();

        // Act
        var refused = await sut.Handle(new SetTeamProductsCommand(team.Id, [kept.Id, added.Id]), TestContext.Current.CancellationToken);
        var allowed = await sut.Handle(new SetTeamProductsCommand(team.Id, [kept.Id]), TestContext.Current.CancellationToken);

        // Assert — the refusal changed nothing; keeping one and dropping the other is allowed
        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be("An inactive team cannot be linked to a product.");
        allowed.IsSuccess.Should().BeTrue();
        kept.TeamLinks.Should().ContainSingle();
        dropped.TeamLinks.Should().BeEmpty();
        added.TeamLinks.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }
}
