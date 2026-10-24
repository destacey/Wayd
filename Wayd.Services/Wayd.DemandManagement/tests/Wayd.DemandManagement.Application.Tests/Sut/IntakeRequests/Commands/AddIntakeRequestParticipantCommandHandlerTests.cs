using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class AddIntakeRequestParticipantCommandHandlerTests
{
    private const string NotAllowed = "Only the desk's reviewers, or the requester where the desk allows it, can add participants.";

    private readonly IntakeScenario _scenario = new();

    private AddIntakeRequestParticipantCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByTheRequesterOnAnOpenDesk_AddsTheParticipant()
    {
        // Arrange
        var requester = _scenario.Employee();
        var colleague = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new AddIntakeRequestParticipantCommand(request.Id, colleague.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Participants.Should().ContainSingle(p => p.EmployeeId == colleague.Id && p.AddedById == requester.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheRequesterOnAPrivateDeskThatDoesNotAllowIt_Fails()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private, requesterMayAddParticipants: false), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new AddIntakeRequestParticipantCommand(request.Id, _scenario.Employee().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotAllowed);
    }

    [Fact]
    public async Task Handle_ByAReviewerOnAPrivateDeskThatDoesNotLetRequestersAdd_AddsTheParticipant()
    {
        // Arrange
        var reviewer = _scenario.Employee();
        var desk = _scenario.Desk(IntakeDeskVisibility.Private, requesterMayAddParticipants: false, reviewerIds: [reviewer.Id]);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(reviewer, IntakeScenario.Review);

        // Act
        var result = await CreateHandler().Handle(new AddIntakeRequestParticipantCommand(request.Id, _scenario.Employee().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ByAParticipant_Fails()
    {
        // Arrange
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee(), participant.Id);
        _scenario.SignedInAs(participant);

        // Act
        var result = await CreateHandler().Handle(new AddIntakeRequestParticipantCommand(request.Id, _scenario.Employee().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotAllowed);
    }

    [Fact]
    public async Task Handle_ForAnInactiveEmployee_Fails()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new AddIntakeRequestParticipantCommand(request.Id, _scenario.Employee(isActive: false).Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A participant must be an active employee.");
    }
}
