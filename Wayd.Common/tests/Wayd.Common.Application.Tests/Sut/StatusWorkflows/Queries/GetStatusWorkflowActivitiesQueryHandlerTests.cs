using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

public class GetStatusWorkflowActivitiesQueryHandlerTests : StatusWorkflowHandlerTestBase, IDisposable
{
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetStatusWorkflowActivitiesQueryHandler _handler;

    public GetStatusWorkflowActivitiesQueryHandlerTests()
    {
        _handler = new GetStatusWorkflowActivitiesQueryHandler(DbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenWorkflowDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetStatusWorkflowActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsTheWorkflowAggregateType_WhenWorkflowExists()
    {
        // Arrange
        var workflow = SeedWorkflow(publish: true);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "WorkflowPublishedEventV2",
                DomainArea = "StatusWorkflows",
                AggregateType = "Workflow",
                AggregateId = workflow.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Workflow Published"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(workflow.Id, "Workflow", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetStatusWorkflowActivitiesQuery(new IdOrKey(workflow.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => DbContext.Dispose();
}
