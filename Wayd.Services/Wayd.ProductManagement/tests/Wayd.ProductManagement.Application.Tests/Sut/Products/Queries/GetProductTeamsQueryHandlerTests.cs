using FluentAssertions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

/// <summary>
/// The teams on a product, rolled up from everything beneath it, so a product line shows every team working
/// on its products.
/// </summary>
public sealed class GetProductTeamsQueryHandlerTests : ProductCommandTestBase
{
    private GetProductTeamsQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ShouldListTheProductsTeams()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var team = SeedTeam("Payments", "PAY", TeamType.TeamOfTeams);
        SeedTeamLink(web, team);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductTeamsQuery(new IdOrKey(web.Id)), TestContext.Current.CancellationToken);

        // Assert
        var row = result.Should().ContainSingle().Subject;
        row.Product.Id.Should().Be(web.Id);
        row.ProductPath.Should().BeEmpty();
        row.Team.Id.Should().Be(team.Id);
        row.Team.Key.Should().Be(team.Key);
        row.Team.Name.Should().Be("Payments");
        row.Team.Code.Should().Be("PAY");
        row.Team.Type.Should().Be(TeamType.TeamOfTeams);
        row.Team.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRollUpLinksFromBeneathTheProduct_NamingWhereEachIsAndNothingOutside()
    {
        // Arrange
        var storefront = SeedProduct("Storefront");
        var web = SeedProduct("Storefront Web", storefront.Id);
        var checkout = SeedProduct("Checkout", web.Id);
        var identity = SeedProduct("Identity Service");
        SeedTeamLink(checkout, SeedTeam("Payments", "PAY"));
        SeedTeamLink(identity, SeedTeam("Identity", "IDN"));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductTeamsQuery(new IdOrKey(storefront.Id)), TestContext.Current.CancellationToken);

        // Assert
        var row = result.Should().ContainSingle().Subject;
        row.Team.Name.Should().Be("Payments");
        row.Product.Id.Should().Be(checkout.Id);
        row.ProductPath.Select(p => p.Name).Should().Equal("Storefront", "Storefront Web");
    }

    [Fact]
    public async Task Handle_ShouldOrderByTeam_OwnLinksBeforeInheritedOnes()
    {
        // Arrange
        var storefront = SeedProduct("Storefront");
        var web = SeedProduct("Storefront Web", storefront.Id);
        var api = SeedProduct("Storefront API", storefront.Id);
        var payments = SeedTeam("Payments", "PAY");
        var atlas = SeedTeam("Atlas", "ATL");
        SeedTeamLink(web, payments);
        SeedTeamLink(storefront, payments);
        SeedTeamLink(api, payments);
        SeedTeamLink(web, atlas);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductTeamsQuery(new IdOrKey(storefront.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.Select(l => (l.Team.Name, l.Product.Name)).Should().Equal(
            ("Atlas", "Storefront Web"),
            ("Payments", "Storefront"),
            ("Payments", "Storefront API"),
            ("Payments", "Storefront Web"));
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductTeamsQuery(new IdOrKey(Guid.CreateVersion7())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
