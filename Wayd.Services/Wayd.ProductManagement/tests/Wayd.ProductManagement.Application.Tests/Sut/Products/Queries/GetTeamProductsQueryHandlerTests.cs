using FluentAssertions;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

public sealed class GetTeamProductsQueryHandlerTests : ProductCommandTestBase
{
    private GetTeamProductsQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ShouldListOnlyThatTeamsProducts_ByProduct()
    {
        // Arrange
        var storefront = SeedProduct("Storefront");
        var web = SeedProduct("Storefront Web", storefront.Id);
        var checkout = SeedProduct("Checkout");
        var payments = SeedTeam("Payments", "PAY");
        SeedTeamLink(web, payments);
        SeedTeamLink(checkout, payments);
        SeedTeamLink(storefront, SeedTeam("Atlas", "ATL"));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamProductsQuery(payments.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Select(l => l.Product.Name).Should().Equal("Checkout", "Storefront Web");
        result.Should().OnlyContain(l => l.Team.Id == payments.Id);
        result.Last().ProductPath.Select(p => p.Name).Should().Equal("Storefront");
    }

    [Fact]
    public async Task Handle_ForATeamWithNoLinks_ShouldReturnAnEmptyList()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamProductsQuery(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
