using FluentAssertions;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class AssignIntakeRequestReviewerCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private AssignIntakeRequestReviewerCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByADeskReviewer_AssignsAnotherReviewerOnTheDesk()
    {
        // Arrange
        var reviewer = _scenario.Employee();
        var assignee = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(reviewerIds: [reviewer.Id, assignee.Id]), _scenario.Employee());
        _scenario.SignedInAs(reviewer, IntakeScenario.Review);

        // Act
        var result = await CreateHandler().Handle(new AssignIntakeRequestReviewerCommand(request.Id, assignee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AssignedReviewerId.Should().Be(assignee.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAReviewHolderNotOnTheDesk_Fails()
    {
        // Arrange
        var outsider = _scenario.Employee();
        var assignee = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(reviewerIds: [assignee.Id]), _scenario.Employee());
        _scenario.SignedInAs(outsider, IntakeScenario.Review);

        // Act
        var result = await CreateHandler().Handle(new AssignIntakeRequestReviewerCommand(request.Id, assignee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAReviewerError);
    }

    [Fact]
    public async Task Handle_ToSomeoneNotReviewingTheDesk_Fails()
    {
        // Arrange
        var reviewer = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(reviewerIds: [reviewer.Id]), _scenario.Employee());
        _scenario.SignedInAs(reviewer, IntakeScenario.Review);

        // Act
        var result = await CreateHandler().Handle(new AssignIntakeRequestReviewerCommand(request.Id, _scenario.Employee().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The assigned reviewer must be an active reviewer on the request's desk.");
    }

    [Fact]
    public async Task Handle_WithNoReviewer_Unassigns()
    {
        // Arrange
        var reviewer = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(reviewerIds: [reviewer.Id]), _scenario.Employee());
        request.AssignReviewer(reviewer.Id, Common.Domain.Events.EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(reviewer, IntakeScenario.Review);

        // Act
        var result = await CreateHandler().Handle(new AssignIntakeRequestReviewerCommand(request.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AssignedReviewerId.Should().BeNull();
    }
}
