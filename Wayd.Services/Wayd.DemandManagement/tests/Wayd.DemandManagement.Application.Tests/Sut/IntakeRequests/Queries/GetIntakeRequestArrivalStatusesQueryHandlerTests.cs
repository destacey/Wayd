using FluentAssertions;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestArrivalStatusesQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestArrivalStatusesQueryHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.Statuses.Object);

    [Fact]
    public async Task Handle_ForAnAgent_OffersTheOpenStatusesNeedingNoReason_DefaultingToTheOneItHolds()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Waiting on Requester");
        _scenario.SignedInAs(agent);

        // Act
        var arrival = await CreateHandler().Handle(new GetIntakeRequestArrivalStatusesQuery(request.Id, _scenario.Desk().Id),
            TestContext.Current.CancellationToken);

        // Assert — Deferred is open but needs a reason, so it is the new desk's call
        arrival!.Statuses.Select(s => s.Name).Should().Equal("Submitted", "In Review", "Waiting on Requester", "Accepted");
        arrival.DefaultStatusId.Should().Be(_scenario.Workflow.Status("Waiting on Requester").Id);
    }

    [Fact]
    public async Task Handle_WhenTheStatusItHoldsCannotArrive_HasNoDefault()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Deferred");
        _scenario.SignedInAs(agent);

        // Act
        var arrival = await CreateHandler().Handle(new GetIntakeRequestArrivalStatusesQuery(request.Id, _scenario.Desk().Id),
            TestContext.Current.CancellationToken);

        // Assert
        arrival!.DefaultStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForSomeoneWhoCannotMoveTheRequest_OffersNothing()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester);

        // Act
        var arrival = await CreateHandler().Handle(new GetIntakeRequestArrivalStatusesQuery(request.Id, _scenario.Desk().Id),
            TestContext.Current.CancellationToken);

        // Assert
        arrival!.Statuses.Should().BeEmpty();
        arrival.DefaultStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var arrival = await CreateHandler().Handle(new GetIntakeRequestArrivalStatusesQuery(request.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        arrival.Should().BeNull();
    }
}
