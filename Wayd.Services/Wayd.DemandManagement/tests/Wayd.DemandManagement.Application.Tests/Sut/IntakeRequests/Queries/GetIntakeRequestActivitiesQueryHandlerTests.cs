using FluentAssertions;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestActivitiesQueryHandlerTests
{
    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _principal = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private IReadOnlyCollection<string>? _requestedEventTypes;

    public GetIntakeRequestActivitiesQueryHandlerTests()
    {
        _activityLogReader
            .Setup(r => r.ReadEventTypes(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, IReadOnlyCollection<string>, int, int, CancellationToken>((_, _, types, _, _, _) => _requestedEventTypes = types)
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([], 0, 1, 50));
    }

    private GetIntakeRequestActivitiesQueryHandler CreateHandler() =>
        new(_dbContext, _principal.Object, _activityLogReader.Object);

    private void SignedInAs(Guid employeeId, bool holdsReview = false)
    {
        _principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        _principal.Setup(p => p.HasPermission(IntakeRequestAuthorization.ReviewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(holdsReview);
    }

    private IntakeRequest RequestOn(IntakeDeskVisibility visibility, params Guid[] reviewerIds)
    {
        var desk = new IntakeDeskFaker().WithVisibility(visibility).WithReviewers(reviewerIds).Generate();
        var request = new IntakeRequestFaker().WithDeskId(desk.Id).Generate();
        _dbContext.AddDesks(desk);
        _dbContext.AddRequests(request);

        return request;
    }

    [Fact]
    public async Task Handle_ForASummaryReader_ReadsOnlySummaryEvents()
    {
        // Arrange
        var request = RequestOn(IntakeDeskVisibility.SummaryOnly);
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _requestedEventTypes.Should().Contain([nameof(IntakeRequestCreatedEvent), nameof(IntakeRequestRetitledEvent)])
            .And.NotContain([nameof(IntakeRequestDetailsUpdatedEvent), nameof(IntakeRequestParticipantAddedEvent)]);
    }

    [Fact]
    public async Task Handle_ForTheRequester_ReadsSummaryAndSubstanceEvents()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var desk = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.SummaryOnly).Generate();
        var request = new IntakeRequestFaker().WithDeskId(desk.Id).WithRequesterId(requesterId).Generate();
        _dbContext.AddDesks(desk);
        _dbContext.AddRequests(request);
        SignedInAs(requesterId);

        // Act
        await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key)), TestContext.Current.CancellationToken);

        // Assert
        _requestedEventTypes.Should().Contain([nameof(IntakeRequestRetitledEvent), nameof(IntakeRequestDetailsUpdatedEvent)]);
    }

    [Fact]
    public async Task Handle_ForSomeoneWhoCannotSeeTheRequest_ReturnsNullAndReadsNothing()
    {
        // Arrange
        var request = RequestOn(IntakeDeskVisibility.Private);
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _activityLogReader.Verify(r => r.ReadEventTypes(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAReviewerOnTheDesk_ReadsTheRequestsOwnHistory()
    {
        // Arrange
        var reviewerId = Guid.NewGuid();
        var request = RequestOn(IntakeDeskVisibility.Private, reviewerId);
        SignedInAs(reviewerId, holdsReview: true);

        // Act
        await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        _activityLogReader.Verify(r => r.ReadEventTypes(request.Id, nameof(IntakeRequest), It.IsAny<IReadOnlyCollection<string>>(),
            1, 50, It.IsAny<CancellationToken>()), Times.Once);
        _requestedEventTypes.Should().Contain(nameof(IntakeRequestDetailsUpdatedEvent));
    }
}
