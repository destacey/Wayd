using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class UntagIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private UntagIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgentOnTheDesk_RemovesAnInactiveTag()
    {
        // Arrange — deactivating a tag stops new use, never removal
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        IntakeScenario.Tagged(request, area, "Payments");
        area.DeactivateTag(area.Tags.Single().Id, EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new UntagIntakeRequestCommand(request.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().BeEmpty();
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagsChangedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAParticipant_FailsAndKeepsTheTag()
    {
        // Arrange
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), _scenario.Employee(), participant.Id);
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        IntakeScenario.Tagged(request, area, "Payments");
        _scenario.SignedInAs(participant);

        // Act
        var result = await CreateHandler().Handle(new UntagIntakeRequestCommand(request.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.Tags.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
