using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server so the objective's references are read through the query the
/// handler sends, not an in-memory list.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetExternalObjectForecastQueryTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ForecastsTheWorkItemsTheObjectReferences()
    {
        // Arrange
        MapsterConfiguration.Ensure();
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        await _seed.AddHistory(team.Id, Ct);
        var objectiveId = Guid.NewGuid();
        var story = await _seed.AddItem(team.Id, Ct, stackRank: 1);
        var epic = await _seed.AddItem(null, Ct, epic: true);
        await _seed.AddItem(team.Id, Ct, stackRank: 2);
        await _seed.AddItem(team.Id, Ct, stackRank: 3, parentId: epic.Id);
        var unrelated = await _seed.AddItem(team.Id, Ct, stackRank: 4);
        await _seed.AddReference(story.Id, objectiveId, Ct);
        await _seed.AddReference(epic.Id, objectiveId, Ct);
        await _seed.AddReference(unrelated.Id, Guid.NewGuid(), Ct);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetExternalObjectForecastQueryHandler(
            accessor.Context,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        // Act
        var forecast = await handler.Handle(
            new GetExternalObjectForecastQuery(objectiveId, ForecastSeeder.Start.PlusDays(1), ForecastOptions.Default),
            Ct);

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.RemainingWorkItems.Should().Be(2);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == ForecastSeeder.Start.PlusDays(2));
        forecast.ChanceOfFinishingByTargetDate.Should().Be(0);
    }
}
