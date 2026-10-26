using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the intake request read handlers against SQL Server: the tier rules translate, a private desk's
/// requests are absent rather than redacted, and a request's history is filtered by tier in the query.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DemandManagementReadTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private sealed record Scenario(
        IntakeDesk Open,
        IntakeDesk SummaryOnly,
        IntakeDesk Private,
        Employee Requester,
        Employee Agent,
        Employee Participant,
        IntakeRequest OnOpen,
        IntakeRequest OnSummaryOnly,
        IntakeRequest OnPrivate);

    [Fact]
    public async Task GetIntakeRequests_ForAnOutsider_LeavesThePrivateDesksRequestsOut()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeRequestsQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetIntakeRequestsQuery(), ct);

        // Assert
        var ids = result.Select(r => r.Id).ToList();
        ids.Should().Contain([scenario.OnOpen.Id, scenario.OnSummaryOnly.Id]).And.NotContain(scenario.OnPrivate.Id);
    }

    [Fact]
    public async Task GetIntakeRequests_ForTheRequesterAndThePrivateDesksAgentWithoutAnyClaim_IncludesThePrivateRequest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var asRequester = await new GetIntakeRequestsQueryHandler(context, Principal(scenario.Requester.Id, holdsView: false))
            .Handle(new GetIntakeRequestsQuery(scenario.Private.Id), ct);
        var asAgent = await new GetIntakeRequestsQueryHandler(context, Principal(scenario.Agent.Id, holdsView: false))
            .Handle(new GetIntakeRequestsQuery(scenario.Private.Id), ct);

        // Assert
        asRequester.Select(r => r.Id).Should().Equal(scenario.OnPrivate.Id);
        asAgent.Select(r => r.Id).Should().Equal(scenario.OnPrivate.Id);
    }

    [Fact]
    public async Task GetIntakeRequests_Mine_ReturnsWhatTheCallerRequestedOrSubmittedAndNothingTheyOnlyParticipateIn()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        Guid onBehalf;
        await using (var seeding = _fixture.CreateContext())
        {
            var desk = await seeding.IntakeDesks.SingleAsync(d => d.Id == scenario.SummaryOnly.Id, ct);
            var channel = await seeding.IntakeChannels.SingleAsync(c => c.Id == scenario.OnOpen.ChannelId, ct);
            var request = IntakeRequest.Create(UniqueName("Ask"), "Entered for someone else", null, scenario.Agent.Id,
                scenario.Participant.Id, desk, channel, null, null, EventActor.System, Now).Value;
            seeding.IntakeRequests.Add(request);
            await seeding.SaveChangesAsync(ct);
            onBehalf = request.Id;
        }

        await using var context = _fixture.CreateContext();

        // Act
        var asSubmitter = await new GetIntakeRequestsQueryHandler(context, Principal(scenario.Participant.Id))
            .Handle(new GetIntakeRequestsQuery(Mine: true), ct);
        var asRequester = await new GetIntakeRequestsQueryHandler(context, Principal(scenario.Requester.Id))
            .Handle(new GetIntakeRequestsQuery(Mine: true), ct);

        // Assert — the participant is on OnOpen but neither requested nor submitted it
        asSubmitter.Select(r => r.Id).Should().Equal(onBehalf);
        asRequester.Select(r => r.Id).Should().BeEquivalentTo(
            [scenario.OnOpen.Id, scenario.OnSummaryOnly.Id, scenario.OnPrivate.Id]);
    }

    [Fact]
    public async Task GetIntakeRequest_ProjectsTheSummary()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeRequestQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetIntakeRequestQuery(new IdOrKey(scenario.OnOpen.Key)), ct);

        // Assert
        result.Should().NotBeNull();
        var summary = result!.Summary;
        summary.Title.Should().Be(scenario.OnOpen.Title);
        summary.Desk.Id.Should().Be(scenario.Open.Id);
        summary.Requester.Id.Should().Be(scenario.Requester.Id);
        summary.Requester.Name.Should().Be("Intake Probe");
        summary.Channel.Id.Should().Be(scenario.OnOpen.ChannelId);
        summary.NeedBy.Should().NotBeNull();
        summary.NeedBy!.Basis.Should().Be(NeedByBasis.Commitment);
        summary.NeedBy.Family.Should().Be(NeedByBasisFamily.ChosenByUs);
        summary.NeedBy.CommittedBy!.Id.Should().Be(scenario.Agent.Id);
        result.CanEdit.Should().BeFalse();
        result.CanRemoveParticipants.Should().BeFalse();
        result.CanDelete.Should().BeFalse();
        result.CanAddParticipants.Should().BeFalse();
    }

    [Fact]
    public async Task GetIntakeRequest_ForAnOutsider_ReturnsSubstanceOnlyOnTheOpenDesk()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();
        var handler = new GetIntakeRequestQueryHandler(context, Principal(Guid.NewGuid()));

        // Act
        var onOpen = await handler.Handle(new GetIntakeRequestQuery(new IdOrKey(scenario.OnOpen.Id)), ct);
        var onSummaryOnly = await handler.Handle(new GetIntakeRequestQuery(new IdOrKey(scenario.OnSummaryOnly.Id)), ct);
        var onPrivate = await handler.Handle(new GetIntakeRequestQuery(new IdOrKey(scenario.OnPrivate.Id)), ct);

        // Assert
        onOpen!.Substance.Should().NotBeNull();
        onOpen.Substance!.Description.Should().Be(scenario.OnOpen.Description);
        onOpen.Substance.SubmittedBy.Id.Should().Be(scenario.Requester.Id);
        onOpen.Substance.Participants.Should().ContainSingle().Which.Employee.Id.Should().Be(scenario.Participant.Id);
        onOpen.Substance.Origin!.SourceSystem.Should().Be("ServicePortal");
        onSummaryOnly!.Substance.Should().BeNull();
        onPrivate.Should().BeNull();
    }

    [Fact]
    public async Task GetIntakeRequest_ForTheRequester_CanEditAndReadsTheSubstance()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeRequestQueryHandler(context, Principal(scenario.Requester.Id))
            .Handle(new GetIntakeRequestQuery(new IdOrKey(scenario.OnSummaryOnly.Id)), ct);

        // Assert
        result!.Substance.Should().NotBeNull();
        result.CanEdit.Should().BeTrue();
        result.CanAddParticipants.Should().BeTrue();
    }

    [Fact]
    public async Task GetIntakeRequest_ForAViewHolderWhoCannotReadTheDetail_CannotEdit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeRequestQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetIntakeRequestQuery(new IdOrKey(scenario.OnSummaryOnly.Id)), ct);

        // Assert — the update carries the description, so the DTO agrees with what the command would refuse
        result!.Substance.Should().BeNull();
        result.CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task GetIntakeRequestActivities_ForASummaryReader_LeavesSubstanceEventsOutOfThePageAndTheCount()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using (var editing = _fixture.CreateContext())
        {
            var request = await editing.IntakeRequests.SingleAsync(r => r.Id == scenario.OnSummaryOnly.Id, ct);
            request.Retitle("Renamed", IntakeRequestEditor.System, Now.Plus(Duration.FromMinutes(1))).IsSuccess.Should().BeTrue();
            request.UpdateDetails("A new description", null, IntakeRequestEditor.System, Now.Plus(Duration.FromMinutes(2))).IsSuccess.Should().BeTrue();
            await editing.SaveChangesAsync(ct);
        }

        await using var context = _fixture.CreateContext();
        var reader = new ActivityLogReader(context);

        // Act
        var asOutsider = await new GetIntakeRequestActivitiesQueryHandler(context, Principal(Guid.NewGuid()), reader)
            .Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(scenario.OnSummaryOnly.Id), new PageRequest()), ct);
        var asRequester = await new GetIntakeRequestActivitiesQueryHandler(context, Principal(scenario.Requester.Id), reader)
            .Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(scenario.OnSummaryOnly.Id), new PageRequest()), ct);

        // Assert
        asOutsider!.Items.Select(a => a.EventType).Should().BeEquivalentTo(
            [nameof(IntakeRequestCreatedEvent), nameof(IntakeRequestRetitledEvent)]);
        asOutsider.TotalCount.Should().Be(2);
        asOutsider.Items.Should().OnlyContain(a => a.Employee == null && a.ClientId == null && !a.Payload.Contains("\"actor\""));
        asRequester!.Items.Select(a => a.EventType).Should().Contain(nameof(IntakeRequestDetailsUpdatedEvent));
    }

    [Fact]
    public async Task GetIntakeDesk_ProjectsItsMembersWithTheirRoles()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeDeskQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetIntakeDeskQuery(new IdOrKey(scenario.Private.Key)), ct);

        // Assert
        result!.Visibility.Should().Be(IntakeDeskVisibility.Private);
        result.Members.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Employee = new { scenario.Agent.Id, Name = "Intake Probe" },
            Role = IntakeDeskRole.Agent,
        });
    }

    [Fact]
    public async Task GetIntakeDesks_CountWhatIsWaitingOnlyForTheDesksMembers()
    {
        // Arrange — a private desk's numbers never reach anyone outside it
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var asAgent = await new GetIntakeDesksQueryHandler(context, Principal(scenario.Agent.Id, holdsView: false))
            .Handle(new GetIntakeDesksQuery(), ct);
        var asOutsider = await new GetIntakeDesksQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetIntakeDesksQuery(), ct);

        // Assert
        var worked = asAgent.Single(d => d.Id == scenario.Private.Id);
        worked.Waiting!.Count.Should().Be(1);
        worked.Waiting.OldestSubmittedOn.Should().NotBeNull();
        worked.CanManage.Should().BeFalse();
        asAgent.Single(d => d.Id == scenario.Open.Id).Waiting.Should().BeNull();
        asOutsider.Single(d => d.Id == scenario.Private.Id).Waiting.Should().BeNull();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task GetIntakeDesk_ForTheDemandManagementAdmin_OffersChangesOnlyWithATokenThatMayWrite(bool isReadOnly, bool expected)
    {
        // Arrange — a read-only token keeps what Administer lets its holder see and loses only the actions
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeDeskQueryHandler(context, Principal(scenario.Agent.Id, holdsAdminister: true, isReadOnly: isReadOnly))
            .Handle(new GetIntakeDeskQuery(new IdOrKey(scenario.Private.Key)), ct);

        // Assert
        result!.Waiting!.Count.Should().Be(1);
        result.CanManage.Should().Be(expected);
        result.CanAdminister.Should().Be(expected);
        result.CanDelete.Should().BeFalse();
    }

    private static ICurrentPrincipal Principal(Guid employeeId, bool holdsView = true, bool holdsAdminister = false,
        bool isReadOnly = false)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.IsReadOnly).Returns(isReadOnly);
        principal.Setup(p => p.HoldsPermission(IntakeRequestAuthorization.AdministerPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holdsAdminister);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        principal.Setup(p => p.HasPermission(IntakeRequestAuthorization.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(holdsView);
        principal.Setup(p => p.HoldsPermission(IntakeRequestAuthorization.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(holdsView);
        return principal.Object;
    }

    private async Task<Scenario> Seed(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();

        var requester = SeedEmployee(context);
        var agent = SeedEmployee(context);
        var participant = SeedEmployee(context);
        await context.SaveChangesAsync(ct);

        IntakeDesk Desk(IntakeDeskVisibility visibility, params Guid[] agents) =>
            IntakeDesk.Create(UniqueName("Desk"), "Anything", visibility, true, true, true,
                [.. agents.Select(id => new IntakeDeskMemberDetails(id, IntakeDeskRole.Agent))], EventActor.System, Now);

        var open = Desk(IntakeDeskVisibility.Open);
        var summaryOnly = Desk(IntakeDeskVisibility.SummaryOnly);
        var @private = Desk(IntakeDeskVisibility.Private, agent.Id);
        var channel = IntakeChannel.Create(UniqueName("Channel"), null, 1);
        context.IntakeDesks.AddRange(open, summaryOnly, @private);
        context.IntakeChannels.Add(channel);
        await context.SaveChangesAsync(ct);

        var needBy = IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.Commitment, "Steering committee",
            agent.Id, new LocalDate(2026, 9, 3)).Value;
        var origin = IntakeRequestOrigin.Create("ServicePortal", UniqueName("REQ"), null).Value;

        IntakeRequest Request(IntakeDesk desk, IntakeRequestNeedBy? withNeedBy = null, IntakeRequestOrigin? withOrigin = null) =>
            IntakeRequest.Create(UniqueName("Ask"), "What is being asked for", null, requester.Id, requester.Id, desk, channel,
                withNeedBy, withOrigin, EventActor.System, Now).Value;

        var onOpen = Request(open, needBy, origin);
        onOpen.AddParticipant(participant.Id, requester.Id, EventActor.System, Now).IsSuccess.Should().BeTrue();
        var onSummaryOnly = Request(summaryOnly);
        var onPrivate = Request(@private);
        context.IntakeRequests.AddRange(onOpen, onSummaryOnly, onPrivate);
        await context.SaveChangesAsync(ct);

        return new Scenario(open, summaryOnly, @private, requester, agent, participant, onOpen, onSummaryOnly, onPrivate);
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

    private static Employee SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Intake", null, "Probe"),
            $"{Guid.NewGuid():N}"[..12],
            Now,
            new EmailAddress($"{Guid.NewGuid():N}"[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Now);

        context.Employees.Add(employee);
        return employee;
    }
}
