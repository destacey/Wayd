using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests;

public sealed class IntakeRequestReadRuleTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();

    public IntakeRequestReadRuleTests()
    {
        TurnDemandManagement(on: true);
        DemandManagementFollowOwners.Register();
    }

    private IntakeRequestReadRule CreateSut() => new(_scenario.DbContext, _scenario.Principal.Object, _featureFlags.Object);

    private void TurnDemandManagement(bool on) =>
        _featureFlags
            .Setup(f => f.IsEnabled(FeatureFlags.Names.DemandManagement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(on);

    private async Task<List<Guid>> ReadableIds(string? search = null)
    {
        var readable = await CreateSut().Readable(search, TestContext.Current.CancellationToken);
        return [.. readable!.Select(r => r.Id)];
    }

    private IntakeRequest AddRequest(IntakeDesk desk, Func<IntakeRequestFaker, IntakeRequestFaker> shape)
    {
        var request = shape(new IntakeRequestFaker().WithDeskId(desk.Id)).Generate();
        _scenario.DbContext.AddRequests(request);
        return request;
    }

    [Fact]
    public void OwnerType_ShouldBeTheRegisteredIntakeRequestKey()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        // Persisted on every follow of a request, so a change here orphans them all.
        ownerType.Should().Be("demand.intake-request");
    }

    [Fact]
    public void ActivityAggregateType_ShouldBeTheTypeTheRequestActivityEndpointReads()
    {
        // Act
        var aggregateType = CreateSut().ActivityAggregateType;

        // Assert
        aggregateType.Should().Be(nameof(IntakeRequest));
    }

    [Fact]
    public async Task Readable_ShouldBeNull_WhenDemandManagementIsOff()
    {
        // Arrange — even the requester, as the request endpoints answer 404 while the flag is off
        TurnDemandManagement(on: false);
        var requester = _scenario.Employee();
        _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester, IntakeScenario.Administer);

        // Act
        var readable = await CreateSut().Readable(null, TestContext.Current.CancellationToken);

        // Assert
        readable.Should().BeNull();
    }

    [Fact]
    public async Task Readable_ForAViewHolder_ShouldLeaveOutAPrivateDesksRequests()
    {
        // Arrange
        var requester = _scenario.Employee();
        var open = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        var summaryOnly = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), requester);
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), requester);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var ids = await ReadableIds();

        // Assert
        ids.Should().BeEquivalentTo([open.Id, summaryOnly.Id]);
    }

    [Fact]
    public async Task Readable_ShouldAdmitAPrivateDesksRequest_ToItsMembersAndItsOwnPeople()
    {
        // Arrange — none of them holds a claim: membership or being named on the request is the grant
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id), requester, participant.Id);

        foreach (var reader in new[] { agent, requester, participant })
        {
            _scenario.SignedInAs(reader);

            // Act
            var ids = await ReadableIds();

            // Assert
            ids.Should().Equal(request.Id);
        }
    }

    [Fact]
    public async Task Readable_WithoutView_ShouldLeaveOutAnOpenDesksRequestsTheReaderIsNotPartOf()
    {
        // Arrange — View is what browses a desk the reader is not on; following never widens it
        var requester = _scenario.Employee();
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var ids = await ReadableIds();

        // Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public async Task Readable_ShouldFlagDoneAndRemovedRequestsClosed_AndNoneUnhealthy()
    {
        // Arrange
        var requester = _scenario.Employee();
        var desk = _scenario.Desk();
        var proposed = _scenario.Request(desk, requester);
        var active = _scenario.RequestIn(desk, requester, StatusCategory.Active);
        var done = _scenario.RequestIn(desk, requester, StatusCategory.Done);
        var removed = _scenario.RequestIn(desk, requester, StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn);
        _scenario.SignedInAs(requester);

        // Act
        var facts = (await CreateSut().Readable(null, TestContext.Current.CancellationToken))!.ToList();

        // Assert
        facts.Where(f => f.IsClosed).Select(f => f.Id).Should().BeEquivalentTo([done.Id, removed.Id]);
        facts.Where(f => !f.IsClosed).Select(f => f.Id).Should().BeEquivalentTo([proposed.Id, active.Id]);
        facts.Should().AllSatisfy(f => f.IsUnhealthy.Should().BeFalse());
    }

    [Fact]
    public async Task Readable_ShouldMatchASearchOnTheTitleOrTheNumberExactly()
    {
        // Arrange
        var desk = _scenario.Desk();
        var byTitle = AddRequest(desk, f => f.WithKey(7).WithTitle("Export invoices"));
        var byNumber = AddRequest(desk, f => f.WithKey(42).WithTitle("Something else"));
        AddRequest(desk, f => f.WithKey(420).WithTitle("Unrelated"));
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var titleMatches = await ReadableIds("invoices");
        var numberMatches = await ReadableIds("42");

        // Assert
        titleMatches.Should().Equal(byTitle.Id);
        numberMatches.Should().Equal(byNumber.Id);
    }

    [Fact]
    public async Task Describe_ShouldReturnWhatTheSummaryShows_UnderItsDesk()
    {
        // Arrange
        var requester = _scenario.Employee();
        var assignee = _scenario.Employee();
        var desk = _scenario.Desk();
        var needBy = IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.RequesterPreference, "Quarter end", null, null).Value;
        var request = AddRequest(desk, f => f
            .WithKey(17)
            .WithTitle("Export invoices")
            .WithRequesterId(requester.Id)
            .WithAssigneeId(assignee.Id)
            .WithNeedBy(needBy)
            .WithStatusName("In Review")
            .InStatus(StatusCategory.Active));

        // Act
        var summaries = await CreateSut().Describe([request.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert — a request that does not exist is absent, not described unnamed
        var summary = summaries.Should().ContainSingle().Subject.Value;
        summary.Key.Should().Be("17");
        summary.Name.Should().Be("Export invoices");
        summary.ParentId.Should().Be(desk.Id);
        summary.ParentKey.Should().Be(desk.Key.ToString(System.Globalization.CultureInfo.InvariantCulture));
        summary.ParentName.Should().Be(desk.Name);
        summary.StatusName.Should().Be("In Review");
        summary.Lifecycle.Should().Be(LifecycleCategory.Active);
        summary.Health.Should().BeNull();
        summary.End.Should().Be(new LocalDate(2026, 12, 31));
        summary.People.Select(p => (p.Role, p.EmployeeId)).Should().Equal(("Assignee", assignee.Id), ("Requester", requester.Id));
    }

    [Theory]
    [InlineData(StatusCategory.Proposed, LifecycleCategory.NotStarted)]
    [InlineData(StatusCategory.Active, LifecycleCategory.Active)]
    [InlineData(StatusCategory.Done, LifecycleCategory.Completed)]
    [InlineData(StatusCategory.Removed, LifecycleCategory.Canceled)]
    public void LifecycleOf_ShouldPlaceEachStatusCategory(StatusCategory category, LifecycleCategory expected)
    {
        // Act
        var lifecycle = IntakeRequestReadRule.LifecycleOf(category);

        // Assert
        lifecycle.Should().Be(expected);
    }

    [Fact]
    public async Task VisibleNotifications_ForASummaryOnlyReader_ShouldWithholdChangesToTheSubstance()
    {
        // Arrange — a View holder following a summary-only desk's request sees its summary and nothing more
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        var follower = _scenario.Employee();
        _scenario.SignedInAs(follower, IntakeScenario.View);
        var notifications = new[]
        {
            Notified(follower, request, NotificationEvent.StatusChanged),
            Notified(follower, request, NotificationEvent.DetailsChanged),
            Notified(follower, request, NotificationEvent.ParticipantsChanged),
        };

        // Act
        var visible = await VisibleEvents(notifications);

        // Assert
        visible.Should().Equal(NotificationEvent.StatusChanged);
    }

    [Fact]
    public async Task VisibleNotifications_ForTheRequestsOwnPeople_ShouldKeepChangesToTheSubstance()
    {
        // Arrange — the same desk, but the reader is a participant, who holds the substance
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee(), participant.Id);
        _scenario.SignedInAs(participant);
        var notifications = new[]
        {
            Notified(participant, request, NotificationEvent.StatusChanged),
            Notified(participant, request, NotificationEvent.DetailsChanged),
        };

        // Act
        var visible = await VisibleEvents(notifications);

        // Assert
        visible.Should().BeEquivalentTo([NotificationEvent.StatusChanged, NotificationEvent.DetailsChanged]);
    }

    [Fact]
    public async Task VisibleNotifications_ForAViewHolderOnAnOpenDesk_ShouldKeepChangesToTheSubstance()
    {
        // Arrange — an open desk shows its substance to everyone with View
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        var follower = _scenario.Employee();
        _scenario.SignedInAs(follower, IntakeScenario.View);

        // Act
        var visible = await VisibleEvents([Notified(follower, request, NotificationEvent.DetailsChanged)]);

        // Assert
        visible.Should().Equal(NotificationEvent.DetailsChanged);
    }

    [Fact]
    public async Task ActorHiddenOn_ShouldHideWhoMadeAChange_OnlyFromAReaderWithoutTheSubstance()
    {
        // Arrange — a View holder reads a summary-only desk's request in summary and an open desk's in full
        var requester = _scenario.Employee();
        var summaryOnly = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), requester);
        var open = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), requester);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var hidden = await CreateSut().ActorHiddenOn([summaryOnly.Id, open.Id], TestContext.Current.CancellationToken);

        // Assert
        hidden.Should().BeEquivalentTo([summaryOnly.Id]);
    }

    [Fact]
    public async Task ActorHiddenOn_ShouldHideNothing_FromTheRequestsOwnPeople()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), requester);
        _scenario.SignedInAs(requester);

        // Act
        var hidden = await CreateSut().ActorHiddenOn([request.Id], TestContext.Current.CancellationToken);

        // Assert
        hidden.Should().BeEmpty();
    }

    [Fact]
    public async Task VisibleNotifications_ShouldWithholdAChangeWithNoTier_FromAnyoneButTheDesk()
    {
        // Arrange — a change the request never notifies of today has no tier, so only an agent sees it
        var requester = _scenario.Employee();
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), requester);

        // Act
        _scenario.SignedInAs(requester);
        var forRequester = await VisibleEvents([Notified(requester, request, NotificationEvent.HealthReported)]);
        _scenario.SignedInAs(agent);
        var forAgent = await VisibleEvents([Notified(agent, request, NotificationEvent.HealthReported)]);

        // Assert
        forRequester.Should().BeEmpty();
        forAgent.Should().Equal(NotificationEvent.HealthReported);
    }

    private async Task<List<NotificationEvent>> VisibleEvents(IEnumerable<Notification> notifications)
    {
        var visible = await CreateSut().VisibleNotifications(notifications.AsQueryable(), TestContext.Current.CancellationToken);
        return [.. visible.Select(n => n.Event)];
    }

    private Notification Notified(Employee recipient, IntakeRequest request, NotificationEvent notificationEvent) =>
        Notification.Create(recipient.Id, DemandManagementFollowOwners.IntakeRequest.Key, request.Id, Guid.NewGuid(),
            notificationEvent, EventActorKind.User, null, _scenario.Clock.Now, NotificationReason.Following).Value;
}
