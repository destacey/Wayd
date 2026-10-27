using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestStatusHistoryQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IStatusHistoryReader> _historyReader = new();

    private GetIntakeRequestStatusHistoryQueryHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _historyReader.Object);

    private void HistoryHasADeclineBy(Guid requestId, Guid agentId) =>
        _historyReader
            .Setup(r => r.Read(DemandManagementWorkflowOwners.IntakeRequest.Key, requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<StatusTransitionDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Sequence = 1,
                    ToStatus = new StatusNavigationDto { Id = Guid.NewGuid(), Name = "Declined", Category = StatusCategory.Removed, Alias = 0 },
                    WorkflowId = _scenario.Workflow.Id,
                    ActorKind = new SimpleNavigationDto { Id = 1, Name = "User" },
                    ChangedBy = new NavigationDto { Id = agentId, Key = 1, Name = "Intake Probe" },
                    ChangedByUser = new UserNavigationDto { Id = Guid.NewGuid(), UserName = "probe", Name = "Intake Probe" },
                    ChangedBySystem = false,
                    ChangedOn = _scenario.Clock.Now,
                    Reason = "Budget went elsewhere",
                },
            }));

    [Fact]
    public async Task Handle_ForTheRequester_ReturnsEveryChangeWithItsReasonAndWhoMadeIt()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), requester);
        HistoryHasADeclineBy(request.Id, Guid.NewGuid());
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestStatusHistoryQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        var transition = result.Value.Should().ContainSingle().Subject;
        transition.Reason.Should().Be("Budget went elsewhere");
        transition.ChangedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ForASummaryReader_LeavesOutTheReasonAndWhoMadeEachChange()
    {
        // Arrange — the reason is substance, and so is the actor: the submitter made the first change
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        HistoryHasADeclineBy(request.Id, Guid.NewGuid());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestStatusHistoryQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        var transition = result.Value.Should().ContainSingle().Subject;
        transition.ToStatus.Name.Should().Be("Declined");
        transition.Reason.Should().BeNull();
        transition.ChangedBy.Should().BeNull();
        transition.ChangedByUser.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForAPrivateRequestTheReaderCannotSee_ReturnsNullWithoutReadingTheHistory()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestStatusHistoryQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeNull();
        _historyReader.VerifyNoOtherCalls();
    }
}
