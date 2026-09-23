using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetWorkItemForecastQueryTests : IDisposable
{
    private readonly ForecastScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    private GetWorkItemForecastQueryHandler Handler() =>
        new(_scenario.Context, _scenario.DateTimeProvider, NullLogger<GetWorkItemForecastQueryHandler>.Instance);

    [Fact]
    public async Task Handle_ForecastsTheWorkItemWithTheKey()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        _scenario.AddItem(team, stackRank: 1);
        var target = _scenario.AddItem(team, stackRank: 2);

        // Act
        var forecast = await Handler().Handle(new GetWorkItemForecastQuery("TEST", target.Key), TestContext.Current.CancellationToken);

        // Assert
        forecast!.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.BacklogPosition.Should().Be(2);
        forecast.TargetDate.Should().BeNull();
        forecast.ChanceOfFinishingByTargetDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UnknownWorkItem_ReturnsNull()
    {
        // Act
        var forecast = await Handler().Handle(new GetWorkItemForecastQuery("TEST", new WorkItemKey("TEST-999")), TestContext.Current.CancellationToken);

        // Assert
        forecast.Should().BeNull();
    }
}
