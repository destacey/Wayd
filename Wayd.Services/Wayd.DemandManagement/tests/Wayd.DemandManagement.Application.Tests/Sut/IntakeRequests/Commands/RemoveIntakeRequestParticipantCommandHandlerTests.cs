using FluentAssertions;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class RemoveIntakeRequestParticipantCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private RemoveIntakeRequestParticipantCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByADeskReviewer_RemovesTheParticipant()
    {
        // Arrange
        var reviewer = _scenario.Employee();
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(reviewerIds: [reviewer.Id]), _scenario.Employee(), participant.Id);
        _scenario.SignedInAs(reviewer, IntakeScenario.Review);

        // Act
        var result = await CreateHandler().Handle(new RemoveIntakeRequestParticipantCommand(request.Id, participant.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Participants.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheRequester_Fails()
    {
        // Arrange — removing a participant needs Review
        var requester = _scenario.Employee();
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester, participant.Id);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new RemoveIntakeRequestParticipantCommand(request.Id, participant.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAReviewerError);
        request.Participants.Should().ContainSingle();
    }
}
