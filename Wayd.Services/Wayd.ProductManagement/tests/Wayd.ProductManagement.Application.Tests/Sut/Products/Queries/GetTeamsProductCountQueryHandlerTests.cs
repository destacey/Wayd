using FluentAssertions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

public sealed class GetTeamsProductCountQueryHandlerTests : ProductCommandTestBase
{
    private GetTeamsProductCountQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ShouldCountEachOpenProductOnceAcrossTheTeams()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var commerce = SeedTeam("Commerce", "COM", TeamType.TeamOfTeams);
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);
        SeedTeamLink(checkout, commerce);
        SeedTeamLink(SeedProduct("Storefront"), commerce);
        SeedTeamLink(SeedProduct("Legacy Cart", status: Status("Retired", StatusCategory.Done, ProductStatusAlias.None)), payments);
        SeedTeamLink(SeedProduct("Atlas"), SeedTeam("Atlas", "ATL"));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamsProductCountQuery([payments.Id, commerce.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ForNoTeams_ShouldReturnZero()
    {
        // Arrange
        SeedTeamLink(SeedProduct(), SeedTeam());
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamsProductCountQuery([]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(0);
    }
}
