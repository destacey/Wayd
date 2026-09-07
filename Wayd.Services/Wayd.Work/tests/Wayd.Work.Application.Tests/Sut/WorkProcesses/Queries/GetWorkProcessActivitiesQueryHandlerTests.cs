using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses.Queries;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Queries;

public class GetWorkProcessActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetWorkProcessActivitiesQueryHandler _handler;
    private readonly WorkProcessFaker _workProcessFaker = new();

    public GetWorkProcessActivitiesQueryHandlerTests()
    {
        _handler = new GetWorkProcessActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenWorkProcessDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetWorkProcessActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsActivities_WhenWorkProcessExists()
    {
        // Arrange
        var workProcess = _workProcessFaker.Generate();
        _dbContext.AddWorkProcess(workProcess);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "IntegrationStateChangedEvent",
                DomainArea = "Work",
                AggregateType = "WorkProcess",
                AggregateId = workProcess.Id,
                ActorKind = EventActorKind.System,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Integration State Changed"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(workProcess.Id, "WorkProcess", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetWorkProcessActivitiesQuery(new IdOrKey(workProcess.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
