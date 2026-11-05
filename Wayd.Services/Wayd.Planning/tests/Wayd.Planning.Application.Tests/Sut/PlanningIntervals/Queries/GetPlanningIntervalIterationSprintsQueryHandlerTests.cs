using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Queries;

public sealed class GetPlanningIntervalIterationSprintsQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly GetPlanningIntervalIterationSprintsQueryHandler _handler;

    public GetPlanningIntervalIterationSprintsQueryHandlerTests()
    {
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetIterationStatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IterationStateDto>());
        _handler = new GetPlanningIntervalIterationSprintsQueryHandler(_dbContext, _dispatcher.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_UnknownPlanningInterval_ReturnsNull()
    {
        // Arrange
        _dbContext.AddPlanningInterval(new PlanningIntervalFaker().WithKey(26).Generate());

        // Act
        var result = await _handler.Handle(new GetPlanningIntervalIterationSprintsQuery("27"), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_KnownPlanningInterval_ReturnsItsIterations()
    {
        // Arrange
        var planningInterval = new PlanningIntervalFaker().WithKey(26).Generate();
        _dbContext.AddPlanningInterval(planningInterval);

        // Act
        var result = await _handler.Handle(new GetPlanningIntervalIterationSprintsQuery("26"), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Select(i => i.Id).Should().BeEquivalentTo(planningInterval.Iterations.Select(i => i.Id));
    }
}
