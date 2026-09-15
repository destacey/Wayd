using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Scoring.ScoringModels.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Queries;

public class GetScoringModelActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly ScoringModelFaker _faker = new();
    private readonly GetScoringModelActivitiesQueryHandler _handler;

    public GetScoringModelActivitiesQueryHandlerTests()
    {
        _handler = new GetScoringModelActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenScoringModelDoesNotExist()
    {
        // Arrange
        var query = new GetScoringModelActivitiesQuery(new IdOrKey(Guid.NewGuid()));

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsTheScoringModelAggregateType_WhenScoringModelExists()
    {
        // Arrange
        var model = _faker.AsProposedWsjf();
        _dbContext.ScoringModels.Add(model);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "ScoringModelActivatedEvent",
                DomainArea = "Scoring",
                AggregateType = "ScoringModel",
                AggregateId = model.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Scoring Model Activated"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(model.Id, "ScoringModel", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetScoringModelActivitiesQuery(new IdOrKey(model.Key), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
