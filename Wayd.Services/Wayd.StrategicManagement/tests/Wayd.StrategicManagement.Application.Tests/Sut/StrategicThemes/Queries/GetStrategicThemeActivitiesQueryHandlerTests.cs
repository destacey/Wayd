using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.StrategicManagement.Application.StrategicThemes.Queries;
using Wayd.StrategicManagement.Application.Tests.Infrastructure;
using Wayd.StrategicManagement.Domain.Tests.Data;

namespace Wayd.StrategicManagement.Application.Tests.Sut.StrategicThemes.Queries;

public class GetStrategicThemeActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeStrategicManagementDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetStrategicThemeActivitiesQueryHandler _handler;
    private readonly StrategicThemeFaker _strategicThemeFaker = new();

    public GetStrategicThemeActivitiesQueryHandlerTests()
    {
        _handler = new GetStrategicThemeActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenStrategicThemeDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetStrategicThemeActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsActivities_WhenStrategicThemeExists()
    {
        // Arrange
        var strategicTheme = _strategicThemeFaker.Generate();
        _dbContext.AddStrategicTheme(strategicTheme);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "StrategicThemeCreatedEvent",
                DomainArea = "StrategicManagement",
                AggregateType = "StrategicTheme",
                AggregateId = strategicTheme.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Strategic Theme Created"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(strategicTheme.Id, "StrategicTheme", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetStrategicThemeActivitiesQuery(new IdOrKey(strategicTheme.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
