using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Models;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Queries;

public sealed class GetPlanningIntervalIterationsQueryHandlerTests : IDisposable
{
    // 1 July in UTC, still 30 June in the system zone.
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 1, 5, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly GetPlanningIntervalIterationsQueryHandler _handler;

    public GetPlanningIntervalIterationsQueryHandlerTests()
    {
        _handler = new GetPlanningIntervalIterationsQueryHandler(
            _dbContext,
            new TestingSystemCalendar(new TestingDateTimeProvider(new FakeClock(Now))));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_IterationStartingOnTheUtcDay_IsFutureUntilThatDayInTheSystemZone()
    {
        // Arrange
        var dates = new LocalDateRange(new LocalDate(2026, 7, 1), new LocalDate(2026, 7, 14));
        var planningInterval = new PlanningIntervalFaker()
            .WithKey(1)
            .WithIterations(dates)
            .Generate();
        _dbContext.AddPlanningInterval(planningInterval);

        // Act
        var result = await _handler.Handle(new GetPlanningIntervalIterationsQuery(new IdOrKey(planningInterval.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.State.Should().Be("Future");
    }
}
