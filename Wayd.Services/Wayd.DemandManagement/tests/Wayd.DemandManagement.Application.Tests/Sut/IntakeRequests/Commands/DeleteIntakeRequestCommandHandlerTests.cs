using FluentAssertions;
using Moq;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class DeleteIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IDiscussionWriter> _discussionWriter = new();

    private DeleteIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _discussionWriter.Object);

    [Fact]
    public async Task Handle_ByADeskAdmin_RecordsTheDeletionAndRemovesIt()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var request = _scenario.Request(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.Employee());
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestDeletedEvent>();
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByADeskAdmin_RemovesItsConversation()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var request = _scenario.Request(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.Employee());
        _scenario.SignedInAs(deskAdmin);

        // Act
        await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        _discussionWriter.Verify(w => w.DeleteChannel("demand.intake-request", request.Id,
            It.Is<EventActor>(a => a.EmployeeId == deskAdmin.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdmin_RemovesIt()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ByAnAgentOnTheDesk_FailsAndKeepsTheRequest()
    {
        // Arrange — deleting is a Desk Admin's last resort; an agent declines instead
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotADeskAdminError);
        _scenario.DbContext.IntakeRequests.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
        _discussionWriter.Verify(w => w.DeleteChannel(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ByTheRequester_Fails()
    {
        // Arrange — withdrawing, not deleting, is the requester's way out
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotADeskAdminError);
        _scenario.DbContext.IntakeRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ForAPrivateRequestTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
        _scenario.DbContext.IntakeRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ForARequestThatHasMovedThroughReview_FailsAndKeepsIt()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var request = _scenario.RequestIn(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.Employee(),
            StatusCategory.Removed);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.PartOfARecordError);
        _scenario.DbContext.IntakeRequests.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DeletesAFreeTagOnlyTheDeletedRequestCarried()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.DeskAdministeredBy(deskAdmin.Id)), _scenario.Employee());
        var free = _scenario.FreeTag();
        _scenario.TaggedFree(request, free);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.DbContext.IntakeRequestTags.Should().NotContain(free);
        free.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestFreeTagDeletedEvent>();
    }

    [Fact]
    public async Task Handle_KeepsAFreeTagAnotherRequestCarries()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.AllowingFreeTags(_scenario.DeskAdministeredBy(deskAdmin.Id));
        var request = _scenario.Request(desk, _scenario.Employee());
        var other = _scenario.Request(desk, _scenario.Employee());
        var free = _scenario.FreeTag();
        _scenario.TaggedFree(request, free);
        _scenario.TaggedFree(other, free);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.DbContext.IntakeRequestTags.Should().Contain(free);
    }
}
