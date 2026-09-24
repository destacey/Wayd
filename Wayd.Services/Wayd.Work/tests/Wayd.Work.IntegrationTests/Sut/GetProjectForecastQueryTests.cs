using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server so project membership — a project of its own, or one inherited
/// from a parent — is read through the query the handler sends.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectForecastQueryTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ForecastsWorkItemsInTheProjectDirectlyOrThroughTheirParent()
    {
        // Arrange
        MapsterConfiguration.Ensure();
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        await _seed.AddHistory(team.Id, Ct);
        var projectId = await _seed.AddProject(Ct);
        var otherProjectId = await _seed.AddProject(Ct);
        await _seed.AddItem(team.Id, Ct, stackRank: 1, projectId: projectId);
        await _seed.AddItem(team.Id, Ct, stackRank: 2, parentProjectId: projectId);
        await _seed.AddItem(team.Id, Ct, stackRank: 3, projectId: otherProjectId, parentProjectId: projectId);
        await _seed.AddItem(team.Id, Ct, stackRank: 4);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetProjectForecastQueryHandler(
            accessor.Context,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        // Act
        var forecast = await handler.Handle(new GetProjectForecastQuery(projectId, null, ForecastOptions.Default), Ct);

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.RemainingWorkItems.Should().Be(2);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == ForecastSeeder.Start.PlusDays(1));
    }
}
