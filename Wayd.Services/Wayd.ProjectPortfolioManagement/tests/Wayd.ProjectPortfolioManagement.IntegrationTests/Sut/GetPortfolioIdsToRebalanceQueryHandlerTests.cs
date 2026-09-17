using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// The rebalance query against a real SQL Server container: the join of projects onto their live
/// portfolios, with the status compared through its enum-to-string converter, has to translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetPortfolioIdsToRebalanceQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    // CreateProject ranks a project one RankStep (1000) above the max it is told; passing a max just
    // under the first project's rank puts the second within a fraction of it.
    private const double RankStep = 1000d;

    [Fact]
    public async Task Handle_FindsTheCrowdedPortfolioAndSkipsTheSpacedOne()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ResetPpmData(cancellationToken);

        Guid crowdedId;
        await using (var context = fixture.CreateContext())
        {
            var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
            await context.ExpenditureCategories.AddAsync(category, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            crowdedId = await SeedPortfolio(context, "Crowded", "CRW", category.Id, secondRankOffset: 0.25, cancellationToken);
            await SeedPortfolio(context, "Spaced", "SPC", category.Id, secondRankOffset: RankStep, cancellationToken);
        }

        await using var queryContext = fixture.CreateContext();
        var handler = new GetPortfolioIdsToRebalanceQueryHandler(queryContext);

        // Act
        var ids = await handler.Handle(new GetPortfolioIdsToRebalanceQuery(), cancellationToken);

        // Assert
        ids.Should().Equal(crowdedId);
    }

    private static async Task<Guid> SeedPortfolio(WaydDbContext context, string name, string keyPrefix, int categoryId, double secondRankOffset, CancellationToken cancellationToken)
    {
        var now = SqlServerDbContextFixture.FixedNow;
        var portfolio = ProjectPortfolio.Create(name, $"{name} portfolio", null, EventActor.System, now);
        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        portfolio.Activate(PpmActor.System, now.InUtc().Date, now);

        var first = portfolio.CreateProject($"{name} One", "First", new ProjectKey($"{keyPrefix}A"), categoryId,
            null, null, null, null, null, null, now, PpmActor.System).Value;
        portfolio.CreateProject($"{name} Two", "Second", new ProjectKey($"{keyPrefix}B"), categoryId,
            null, null, null, null, null, null, now, PpmActor.System, currentMaxRank: first.Rank + secondRankOffset - RankStep)
            .IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync(cancellationToken);
        return portfolio.Id;
    }
}
