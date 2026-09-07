using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Queries;

public class GetReleaseActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetReleaseActivitiesQueryHandler _handler;
    private readonly ReleaseFaker _releaseFaker = new();

    public GetReleaseActivitiesQueryHandlerTests()
    {
        _handler = new GetReleaseActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenReleaseDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetReleaseActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsActivities_WhenReleaseExists()
    {
        // Arrange
        var release = _releaseFaker.Generate();
        _dbContext.AddRelease(release);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "ReleasePlannedEvent",
                DomainArea = "ProductManagement",
                AggregateType = "Release",
                AggregateId = release.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Release Planned"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(release.Id, "Release", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetReleaseActivitiesQuery(new IdOrKey(release.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
