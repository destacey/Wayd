using FluentAssertions;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetProjectForecastQueryTests : IDisposable
{
    private readonly ForecastScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    private GetProjectForecastQueryHandler Handler() => new(_scenario.Context, _scenario.DateTimeProvider);

    [Fact]
    public async Task Handle_ForecastsWorkItemsInTheProjectDirectlyOrThroughTheirParent()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var projectId = Guid.NewGuid();
        _scenario.AddItem(team, stackRank: 1, projectId: projectId);
        _scenario.AddItem(team, stackRank: 2, parentProjectId: projectId);
        _scenario.AddItem(team, stackRank: 3, projectId: Guid.NewGuid(), parentProjectId: projectId);
        _scenario.AddItem(team, stackRank: 4);
        var targetDate = ForecastScenario.Start.PlusDays(1);

        // Act
        var forecast = await Handler().Handle(new GetProjectForecastQuery(projectId, targetDate, ForecastOptions.Default), TestContext.Current.CancellationToken);

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.RemainingWorkItems.Should().Be(2);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == ForecastScenario.Start.PlusDays(1));
        forecast.ChanceOfFinishingByTargetDate.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ProjectWithNoWorkItems_HasNothingRemaining()
    {
        // Act
        var forecast = await Handler().Handle(new GetProjectForecastQuery(Guid.NewGuid(), null, ForecastOptions.Default), TestContext.Current.CancellationToken);

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.NoRemainingWork);
    }
}
