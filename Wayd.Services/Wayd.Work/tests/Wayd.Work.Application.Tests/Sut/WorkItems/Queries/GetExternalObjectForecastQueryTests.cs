using FluentAssertions;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetExternalObjectForecastQueryTests : IDisposable
{
    private readonly ForecastScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    private GetExternalObjectForecastQueryHandler Handler() => new(_scenario.Context, _scenario.DateTimeProvider);

    [Fact]
    public async Task Handle_ForecastsOnlyTheWorkItemsTheObjectReferences()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var objectiveId = Guid.NewGuid();
        var referenced = _scenario.AddItem(team, stackRank: 1);
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        _scenario.AddItem(team, stackRank: 3, parentId: epic.Id);
        _scenario.AddItem(team, stackRank: 2);
        _scenario.AddReference(referenced, objectiveId);
        _scenario.AddReference(epic, objectiveId);
        _scenario.AddReference(_scenario.AddItem(team, stackRank: 4), Guid.NewGuid());
        var targetDate = ForecastScenario.Start.PlusDays(1);

        // Act
        var forecast = await Handler().Handle(new GetExternalObjectForecastQuery(objectiveId, targetDate, ForecastOptions.Default), TestContext.Current.CancellationToken);

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.RemainingWorkItems.Should().Be(2);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == ForecastScenario.Start.PlusDays(2));
        forecast.TargetDate.Should().Be(targetDate);
        forecast.ChanceOfFinishingByTargetDate.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ObjectWithNoWorkItems_HasNothingRemaining()
    {
        // Act
        var forecast = await Handler().Handle(new GetExternalObjectForecastQuery(Guid.NewGuid(), null, ForecastOptions.Default), TestContext.Current.CancellationToken);

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.NoRemainingWork);
    }
}
