using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives.Queries;

public class GetStrategicInitiativeActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetStrategicInitiativeActivitiesQueryHandler _handler;
    private readonly StrategicInitiativeFaker _initiativeFaker =
        new(new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant())));

    public GetStrategicInitiativeActivitiesQueryHandlerTests()
    {
        _handler = new GetStrategicInitiativeActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenStrategicInitiativeDoesNotExist()
    {
        // Arrange
        var query = new GetStrategicInitiativeActivitiesQuery(new IdOrKey(Guid.NewGuid()));

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsTheInitiativesOwnHistory_WhenStrategicInitiativeExists()
    {
        // Arrange
        var initiative = _initiativeFaker.Generate();
        _dbContext.AddStrategicInitiative(initiative);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = 1,
                EventType = "StrategicInitiativeCreatedEvent",
                DomainArea = "Ppm",
                AggregateType = "StrategicInitiative",
                AggregateId = initiative.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Strategic Initiative Created"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(initiative.Id, "StrategicInitiative", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetStrategicInitiativeActivitiesQuery(new IdOrKey(initiative.Key), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
