using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Application.EstimationScales.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.EstimationScales.Queries;

public class GetEstimationScalesQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();

    private GetEstimationScalesQueryHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Handle_ShouldReturnActiveScalesByName_WithTheirCurrentVersion()
    {
        // Arrange
        var sizes = new EstimationScaleFaker().WithName("Sizes").WithValues("S", "M", "?").Generate();
        sizes.PublishVersion([new EstimationScaleValue("S", true), new EstimationScaleValue("M", true), new EstimationScaleValue("L", true)], EventActor.System, Instant.FromUtc(2026, 2, 1, 12, 0));
        var fibonacci = new EstimationScaleFaker().WithName("Fibonacci").Generate();
        var retired = new EstimationScaleFaker().WithName("Alpha").WithIsActive(false).Generate();
        _dbContext.AddEstimationScales([sizes, fibonacci, retired]);

        // Act
        var result = await CreateHandler().Handle(new GetEstimationScalesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(s => s.Name).Should().Equal("Fibonacci", "Sizes");
        var current = result.Single(s => s.Id == sizes.Id).CurrentVersion;
        current.Number.Should().Be(2);
        current.Values.Select(v => v.Value).Should().Equal("S", "M", "L");
    }

    [Fact]
    public async Task Handle_IncludeInactive_ShouldReturnInactiveScalesToo()
    {
        // Arrange
        var active = new EstimationScaleFaker().WithName("Beta").Generate();
        var retired = new EstimationScaleFaker().WithName("Alpha").WithIsActive(false).Generate();
        _dbContext.AddEstimationScales([active, retired]);

        // Act
        var result = await CreateHandler().Handle(new GetEstimationScalesQuery(IncludeInactive: true), TestContext.Current.CancellationToken);

        // Assert
        result.Select(s => s.Id).Should().Equal(retired.Id, active.Id);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
