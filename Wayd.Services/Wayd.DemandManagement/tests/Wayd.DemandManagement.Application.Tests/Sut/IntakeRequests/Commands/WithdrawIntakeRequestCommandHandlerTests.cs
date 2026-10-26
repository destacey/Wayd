using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class WithdrawIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private WithdrawIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _scenario.Statuses.Object);

    [Fact]
    public async Task Handle_ByTheRequesterWithNoClaim_WithdrawsIt()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new WithdrawIntakeRequestCommand(request.Id, "No longer needed"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusAlias.Should().Be(IntakeRequestStatusAlias.Withdrawn);
        request.StatusReason.Should().Be("No longer needed");
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestWithdrawnEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheSubmitterOnTheirBehalf_Fails()
    {
        // Arrange
        var submitter = _scenario.Employee();
        var onBehalf = _scenario.RequestOnBehalf(_scenario.Desk(), _scenario.Employee(), submitter);
        _scenario.SignedInAs(submitter);

        // Act
        var result = await CreateHandler().Handle(new WithdrawIntakeRequestCommand(onBehalf.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only the requester can withdraw their request.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByAnAgentOnTheDesk_Fails()
    {
        // Arrange — the desk declines; it does not withdraw on the requester's behalf
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new WithdrawIntakeRequestCommand(request.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        request.StatusAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
    }

    [Fact]
    public async Task Handle_ForAPrivateRequestTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new WithdrawIntakeRequestCommand(request.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }
}
