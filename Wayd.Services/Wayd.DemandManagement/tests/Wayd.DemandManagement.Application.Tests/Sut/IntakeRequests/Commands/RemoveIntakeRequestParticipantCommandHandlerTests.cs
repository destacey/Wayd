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
    public async Task Handle_ByAnAgentOnTheDesk_RemovesTheParticipant()
    {
        // Arrange
        var agent = _scenario.Employee();
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee(), participant.Id);
        _scenario.SignedInAs(agent);

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
        // Arrange — removing a participant needs an agent on the desk, which the requester is not
        var requester = _scenario.Employee();
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester, participant.Id);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new RemoveIntakeRequestParticipantCommand(request.Id, participant.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.Participants.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
