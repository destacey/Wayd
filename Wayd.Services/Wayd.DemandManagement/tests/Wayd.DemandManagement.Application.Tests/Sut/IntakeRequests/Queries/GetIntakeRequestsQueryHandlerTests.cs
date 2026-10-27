using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestsQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_Mine_IncludesWhatTheCallerSubmittedForSomeoneElse()
    {
        // Arrange
        var caller = _scenario.Employee();
        var someoneElse = _scenario.Employee();
        var desk = _scenario.Desk(IntakeDeskVisibility.SummaryOnly);
        var onBehalf = _scenario.RequestOnBehalf(desk, someoneElse, caller);
        var requested = _scenario.Request(desk, caller);
        _scenario.Request(desk, someoneElse);
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Id).Should().BeEquivalentTo([onBehalf.Id, requested.Id]);
    }

    [Fact]
    public async Task Handle_Mine_LeavesOutRequestsTheCallerOnlyParticipatesIn()
    {
        // Arrange
        var caller = _scenario.Employee();
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee(), caller.Id);
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MineWithNoLinkedEmployee_ReturnsNothing()
    {
        // Arrange
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.Principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutView_ListsOnlyTheCallersDesksAndOwnRequests()
    {
        // Arrange — browsing other people's requests is what View grants; desk work and one's own asks need no claim
        var caller = _scenario.Employee();
        var desk = _scenario.Desk(IntakeDeskVisibility.Private, agentIds: caller.Id);
        var onTheirDesk = _scenario.Request(desk, _scenario.Employee());
        var theirOwn = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), caller);
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Id).Should().BeEquivalentTo([onTheirDesk.Id, theirOwn.Id]);
    }

    [Fact]
    public async Task Handle_WithView_AlsoListsOtherOpenDesksButNoOtherPrivateOnes()
    {
        // Arrange
        var caller = _scenario.Employee();
        var onOpenDesk = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        var onSummaryOnlyDesk = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(caller, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Id).Should().BeEquivalentTo([onOpenDesk.Id, onSummaryOnlyDesk.Id]);
    }

    [Fact]
    public async Task Handle_ForAnAgentWhoHasLeft_ListsNothingFromTheirFormerDesk()
    {
        // Arrange — an inactive employee is a member of no desk, so offboarding ends desk access
        var leaver = _scenario.Employee(isActive: false);
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: leaver.Id), _scenario.Employee());
        _scenario.SignedInAs(leaver);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OpenOnly_LeavesOutDoneAndRemovedRequests()
    {
        // Arrange — open is the Proposed and Active categories, whatever the desk's workflow names the status
        var desk = _scenario.Desk(IntakeDeskVisibility.Open);
        var proposed = _scenario.Request(desk, _scenario.Employee());
        var active = _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Active, IntakeRequestStatusAlias.Accepted);
        _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Done);
        _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Removed, IntakeRequestStatusAlias.Declined);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(OpenOnly: true), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Id).Should().BeEquivalentTo([proposed.Id, active.Id]);
    }

    [Fact]
    public async Task Handle_ReadsWhenARequestArrivedAndWhenItEnteredItsStatusFromItsHistory()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        var submittedOn = _scenario.Clock.Now;
        _scenario.RecordMove(request, "In Review", submittedOn + Duration.FromDays(2));
        var lastMovedOn = submittedOn + Duration.FromDays(5);
        _scenario.RecordMove(request, "Waiting on Requester", lastMovedOn);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var summary = result.Should().ContainSingle().Subject;
        summary.SubmittedOn.Should().Be(submittedOn);
        summary.StatusChangedOn.Should().Be(lastMovedOn);
    }
}
