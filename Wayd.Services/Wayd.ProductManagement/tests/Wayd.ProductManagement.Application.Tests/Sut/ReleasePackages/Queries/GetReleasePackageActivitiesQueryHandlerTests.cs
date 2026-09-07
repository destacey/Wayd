using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ReleasePackages.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Queries;

public class GetReleasePackageActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetReleasePackageActivitiesQueryHandler _handler;
    private readonly ReleasePackageFaker _releasePackageFaker = new();

    public GetReleasePackageActivitiesQueryHandlerTests()
    {
        _handler = new GetReleasePackageActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenReleasePackageDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetReleasePackageActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsActivities_WhenReleasePackageExists()
    {
        // Arrange
        var releasePackage = _releasePackageFaker.Generate();
        _dbContext.AddReleasePackage(releasePackage);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "PackageAssembledEvent",
                DomainArea = "ProductManagement",
                AggregateType = "ReleasePackage",
                AggregateId = releasePackage.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Package Assembled"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(releasePackage.Id, "ReleasePackage", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetReleasePackageActivitiesQuery(new IdOrKey(releasePackage.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
