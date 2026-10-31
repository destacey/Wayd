using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class LinkProductTeamCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IDispatcher> _dispatcher = new();

    private LinkProductTeamCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, _dispatcher.Object, Logger<LinkProductTeamCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldLinkTheTeam()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var team = SeedTeam();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new LinkProductTeamCommand(web.Id, team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(team.Id);
        web.DomainEvents.OfType<ProductTeamLinkedEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheTeamIsAlreadyLinked_ShouldSucceedWithoutAnotherLink()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var team = SeedTeam();
        SeedTeamLink(web, team);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new LinkProductTeamCommand(web.Id, team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.TeamLinks.Should().ContainSingle();
        web.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheTeamHasNoCopyYet_ShouldCopyItFromOrganizationAndLinkIt()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var source = new TeamSource(Guid.CreateVersion7(), 42, "Payments", new TeamCode("PAY"), TeamType.TeamOfTeams, true);
        _dispatcher
            .Setup(d => d.Send(It.Is<GetSimpleTeamQuery>(q => q.Id == source.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new LinkProductTeamCommand(web.Id, source.Id), TestContext.Current.CancellationToken);

        // Assert — the copy is saved on its own before the link, so a racing copy cannot fail the link's save
        result.IsSuccess.Should().BeTrue();
        var copy = DbContext.ProductTeams.Should().ContainSingle().Subject;
        copy.Id.Should().Be(source.Id);
        copy.Name.Should().Be("Payments");
        copy.Type.Should().Be(TeamType.TeamOfTeams);
        web.TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(source.Id);
        DbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenTheTeamHasNoCopyYet_ShouldStampTheCopyWithTheTimeBeforeTheRead()
    {
        // Arrange — the clock moves on while Organization is read. A change committed in that gap must arrive as
        // newer than the copy, or the copy would skip it as stale and keep what the read saw.
        var web = SeedProduct("Storefront Web");
        var source = new TeamSource(Guid.CreateVersion7(), 42, "Payments", new TeamCode("PAY"), TeamType.Team, true);
        var afterTheRead = Now.Plus(Duration.FromSeconds(5));
        _dispatcher
            .Setup(d => d.Send(It.Is<GetSimpleTeamQuery>(q => q.Id == source.Id), It.IsAny<CancellationToken>()))
            .Callback(() => DateTimeProvider.SetupGet(p => p.Now).Returns(afterTheRead))
            .ReturnsAsync(source);
        var sut = CreateSut();

        // Act
        await sut.Handle(new LinkProductTeamCommand(web.Id, source.Id), TestContext.Current.CancellationToken);

        // Assert
        DbContext.ProductTeams.Should().ContainSingle()
            .Which.Watermarks.Should().Be(TeamReplicaWatermarks.At(Now));
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
        var result = await sut.Handle(new LinkProductTeamCommand(web.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Team not found.");
        web.TeamLinks.Should().BeEmpty();
        DbContext.ProductTeams.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheAggregateRefuses_ShouldFailWithoutSaving()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var team = SeedTeam(isActive: false);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new LinkProductTeamCommand(web.Id, team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An inactive team cannot be linked to a product.");
        web.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var team = SeedTeam();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new LinkProductTeamCommand(Guid.CreateVersion7(), team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
