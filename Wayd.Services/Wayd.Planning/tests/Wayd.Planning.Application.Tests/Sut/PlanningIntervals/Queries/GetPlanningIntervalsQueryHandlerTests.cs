using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Queries;

public sealed class GetPlanningIntervalsQueryHandlerTests : IDisposable
{
    // 1 July in UTC, still 30 June in the system zone.
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 1, 5, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly GetPlanningIntervalsQueryHandler _handler;

    public GetPlanningIntervalsQueryHandlerTests()
    {
        _handler = new GetPlanningIntervalsQueryHandler(_dbContext, new TestingSystemCalendar(new TestingDateTimeProvider(new FakeClock(Now))));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_PlanningIntervalStartingOnTheUtcDay_IsFutureUntilThatDayInTheSystemZone()
    {
        // Arrange
        _dbContext.AddPlanningInterval(new PlanningIntervalFaker()
            .WithKey(1)
            .WithDateRange(new LocalDateRange(new LocalDate(2026, 7, 1), new LocalDate(2026, 9, 30)))
            .Generate());

        // Act
        var result = await _handler.Handle(new GetPlanningIntervalsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.State.Id.Should().Be((int)IterationState.Future);
    }
}
