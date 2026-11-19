using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Queries;

public sealed class GetPlanningIntervalQueryHandlerTests : IDisposable
{
    // 1 July in UTC, still 30 June in the system zone.
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 1, 5, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly GetPlanningIntervalQueryHandler _handler;

    public GetPlanningIntervalQueryHandlerTests()
    {
        _handler = new GetPlanningIntervalQueryHandler(
            _dbContext,
            NullLogger<GetPlanningIntervalQueryHandler>.Instance,
            new TestingSystemCalendar(new TestingDateTimeProvider(new FakeClock(Now))));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_PlanningIntervalStartingOnTheUtcDay_IsFutureUntilThatDayInTheSystemZone()
    {
        // Arrange
        var planningInterval = new PlanningIntervalFaker()
            .WithKey(1)
            .WithDateRange(new LocalDateRange(new LocalDate(2026, 7, 1), new LocalDate(2026, 9, 30)))
            .Generate();
        _dbContext.AddPlanningInterval(planningInterval);

        // Act
        var result = await _handler.Handle(new GetPlanningIntervalQuery(new IdOrKey(planningInterval.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.State.Id.Should().Be((int)IterationState.Future);
    }
}
