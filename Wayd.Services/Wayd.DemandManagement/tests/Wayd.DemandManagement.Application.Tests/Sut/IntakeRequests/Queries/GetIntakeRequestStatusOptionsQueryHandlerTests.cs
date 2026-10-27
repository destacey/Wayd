using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestStatusOptionsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestStatusOptionsQueryHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.Statuses.Object);

    [Fact]
    public async Task Handle_ForAnAgent_OffersEveryOtherStatusButWithdrawn_WithWhatEachNeeds()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var options = await CreateHandler().Handle(new GetIntakeRequestStatusOptionsQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert — in the workflow's order, without the status it holds
        options!.Select(o => o.Name).Should().Equal(
            "In Review", "Waiting on Requester", "Accepted", "Fulfilled", "Declined", "Duplicate");
        options.Where(o => o.NeedsReason).Select(o => o.Name).Should().BeEquivalentTo(["Fulfilled", "Declined", "Duplicate"]);
        options.Where(o => o.NeedsSurvivingRequest).Select(o => o.Name).Should().Equal("Duplicate");
    }

    [Fact]
    public async Task Handle_ForTheRequester_OffersNothing()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester);

        // Act
        var options = await CreateHandler().Handle(new GetIntakeRequestStatusOptionsQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        options.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_ForARequestTheReaderCannotSee_ReturnsNull()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var options = await CreateHandler().Handle(new GetIntakeRequestStatusOptionsQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        options.Should().BeNull();
    }
}
