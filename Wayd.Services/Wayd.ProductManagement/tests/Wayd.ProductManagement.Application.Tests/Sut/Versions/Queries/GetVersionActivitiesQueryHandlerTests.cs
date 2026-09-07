using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Application.Versions.Queries;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Queries;

public class GetVersionActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetVersionActivitiesQueryHandler _handler;
    private readonly VersionFaker _versionFaker = new();

    public GetVersionActivitiesQueryHandlerTests()
    {
        _handler = new GetVersionActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenVersionDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetVersionActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsActivities_WhenVersionExists()
    {
        // Arrange
        var version = _versionFaker.Generate();
        _dbContext.AddVersion(version);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "VersionCutEvent",
                DomainArea = "ProductManagement",
                AggregateType = "Version",
                AggregateId = version.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Version Cut"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(version.Id, "Version", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetVersionActivitiesQuery(new IdOrKey(version.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
