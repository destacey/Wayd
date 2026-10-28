using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Queries;
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
    public async Task GetIntakeRequests_ReadsArrivalAndTimeInStatusFromTheRequestsHistory()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        var movedOn = Now.Plus(Duration.FromDays(3));
        await using (var arrange = _fixture.CreateContext())
        {
            var request = await arrange.IntakeRequests.SingleAsync(r => r.Id == scenario.OnOpen.Id, ct);
            var inReview = new StatusRef(request.StatusWorkflowId, Guid.CreateVersion7(), "In Review", StatusCategory.Proposed,
                StatusWorkflow.NoAlias);
            request.ChangeStatus(inReview, null, null, EventActor.System, movedOn).IsSuccess.Should().BeTrue();
            await arrange.SaveChangesAsync(ct);
        }
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeRequestsQueryHandler(context, Principal(scenario.Agent.Id))
            .Handle(new GetIntakeRequestsQuery(scenario.Open.Id), ct);

        // Assert
        var summary = result.Should().ContainSingle().Subject;
        summary.SubmittedOn.Should().Be(Now);
        summary.StatusChangedOn.Should().Be(movedOn);
    }

    [Fact]
    public async Task GetIntakeRequests_OpenOnly_LeavesOutAClosedRequest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using (var arrange = _fixture.CreateContext())
        {
            var desk = await arrange.IntakeDesks.SingleAsync(d => d.Id == scenario.Open.Id, ct);
            var channel = await arrange.IntakeChannels.FirstAsync(ct);
            var closed = IntakeRequest.Create(UniqueName("Ask"), "Already decided", null, scenario.Requester.Id,
                scenario.Requester.Id, desk, channel, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
            var declined = new StatusRef(closed.StatusWorkflowId, Guid.CreateVersion7(), "Declined", StatusCategory.Removed,
                StatusWorkflow.NoAlias);
            closed.ChangeStatus(declined, "Not this year", null, EventActor.System, Now).IsSuccess.Should().BeTrue();
            arrange.IntakeRequests.Add(closed);
            await arrange.SaveChangesAsync(ct);
        }
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeRequestsQueryHandler(context, Principal(scenario.Agent.Id))
            .Handle(new GetIntakeRequestsQuery(scenario.Open.Id, OpenOnly: true), ct);

        // Assert
        result.Select(r => r.Id).Should().Equal(scenario.OnOpen.Id);
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
                scenario.Participant.Id, desk, channel, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
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
        summary.Channel!.Id.Should().Be(scenario.OnOpen.ChannelId!.Value);
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
            [nameof(IntakeRequestCreatedEventV2), nameof(IntakeRequestRetitledEvent)]);
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

    [Fact]
    public async Task GetIntakeRequests_ProjectsTheRequestsTagsInAxisOrderThenByName()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using (var arrange = _fixture.CreateContext())
        {
            var first = IntakeRequestTagCategory.Create(UniqueName("Area"), null, allowsMany: true, -2, EventActor.System, Now);
            var second = IntakeRequestTagCategory.Create(UniqueName("Segment"), null, allowsMany: false, -1, EventActor.System, Now);
            var payments = first.AddTag("Payments", null, EventActor.System, Now).Value;
            var billing = first.AddTag("Billing", null, EventActor.System, Now).Value;
            var enterprise = second.AddTag("Enterprise", null, EventActor.System, Now).Value;
            arrange.IntakeRequestTagCategories.AddRange(first, second);
            await arrange.SaveChangesAsync(ct);

            var request = await arrange.IntakeRequests.Include(r => r.Tags).SingleAsync(r => r.Id == scenario.OnOpen.Id, ct);
            var desk = await arrange.IntakeDesks.Include(d => d.TagCategories).SingleAsync(d => d.Id == request.DeskId, ct);
            request.Tag(enterprise, second, desk, EventActor.System, Now).IsSuccess.Should().BeTrue();
            request.Tag(payments, first, desk, EventActor.System, Now).IsSuccess.Should().BeTrue();
            request.Tag(billing, first, desk, EventActor.System, Now).IsSuccess.Should().BeTrue();
            await arrange.SaveChangesAsync(ct);
        }
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new GetIntakeRequestsQueryHandler(context, Principal(scenario.Agent.Id))
            .Handle(new GetIntakeRequestsQuery(scenario.Open.Id), ct);

        // Assert
        result.Should().ContainSingle().Which.Tags.Select(t => t.Name).Should().Equal("Billing", "Payments", "Enterprise");
    }

    [Fact]
    public async Task GetIntakeRequestTagCategories_ForAnOutsider_CountsNoRequestOnAPrivateDesk()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        var name = UniqueName("Area");
        await using (var arrange = _fixture.CreateContext())
        {
            var area = IntakeRequestTagCategory.Create(name, null, allowsMany: true, 1, EventActor.System, Now);
            var payments = area.AddTag("Payments", null, EventActor.System, Now).Value;
            arrange.IntakeRequestTagCategories.Add(area);
            await arrange.SaveChangesAsync(ct);

            var requests = await arrange.IntakeRequests.Include(r => r.Tags)
                .Where(r => r.Id == scenario.OnOpen.Id || r.Id == scenario.OnPrivate.Id)
                .ToListAsync(ct);
            foreach (var request in requests)
            {
                var desk = await arrange.IntakeDesks.Include(d => d.TagCategories).SingleAsync(d => d.Id == request.DeskId, ct);
                request.Tag(payments, area, desk, EventActor.System, Now).IsSuccess.Should().BeTrue();
            }
            await arrange.SaveChangesAsync(ct);
        }
        await using var context = _fixture.CreateContext();

        // Act
        var outsider = await new GetIntakeRequestTagCategoriesQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetIntakeRequestTagCategoriesQuery(), ct);
        var admin = await new GetIntakeRequestTagCategoriesQueryHandler(context, Principal(Guid.NewGuid(), holdsAdminister: true))
            .Handle(new GetIntakeRequestTagCategoriesQuery(), ct);

        // Assert
        outsider.Single(c => c.Name == name).Tags.Single().RequestCount.Should().Be(1);
        admin.Single(c => c.Name == name).Tags.Single().RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task Outcomes_ReadBackOnTheRequestAndFromTheProject_UnderTheDesksVisibility()
    {
        // Arrange — the same project absorbed an open request and a private one; a second outcome on the open
        // request points at a project whose copy has gone
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        var projectId = Guid.CreateVersion7();
        var deletedProjectId = Guid.CreateVersion7();
        await using (var arrange = _fixture.CreateContext())
        {
            arrange.DemandProjects.Add(new DemandProject(new Source(projectId, new ProjectKey("P" + Guid.NewGuid().ToString("N")[..10]), "Atlas"), Now));
            var requests = await arrange.IntakeRequests.Include(r => r.Outcomes)
                .Where(r => r.Id == scenario.OnOpen.Id || r.Id == scenario.OnPrivate.Id)
                .ToListAsync(ct);
            foreach (var request in requests)
            {
                var accepted = new StatusRef(request.StatusWorkflowId, Guid.CreateVersion7(), "Accepted", StatusCategory.Active,
                    (int)IntakeRequestStatusAlias.Accepted);
                request.ChangeStatus(accepted, null, null, EventActor.System, Now).IsSuccess.Should().BeTrue();
                request.AddOutcome(IntakeRequestOutcomeKind.ExistingProjectScope, projectId, "Export screen", scenario.Agent.Id,
                    EventActor.System, Now).IsSuccess.Should().BeTrue();
            }
            requests.Single(r => r.Id == scenario.OnOpen.Id)
                .AddOutcome(IntakeRequestOutcomeKind.NewProject, deletedProjectId, null, scenario.Agent.Id, EventActor.System, Now)
                .IsSuccess.Should().BeTrue();
            await arrange.SaveChangesAsync(ct);
        }
        await using var context = _fixture.CreateContext();

        // Act
        var fromProject = await new GetProjectIntakeRequestsQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetProjectIntakeRequestsQuery(projectId), ct);
        var fromProjectAsAgent = await new GetProjectIntakeRequestsQueryHandler(context, Principal(scenario.Agent.Id))
            .Handle(new GetProjectIntakeRequestsQuery(projectId), ct);
        var onRequest = await new GetIntakeRequestQueryHandler(context, Principal(Guid.NewGuid()))
            .Handle(new GetIntakeRequestQuery(new IdOrKey(scenario.OnOpen.Id)), ct);

        // Assert
        var listed = fromProject.Should().ContainSingle().Subject;
        listed.Request.Id.Should().Be(scenario.OnOpen.Id);
        listed.Kind.Should().Be(IntakeRequestOutcomeKind.ExistingProjectScope);
        listed.Note.Should().Be("Export screen");
        listed.Status.Name.Should().Be("Accepted");
        fromProjectAsAgent.Select(r => r.Request.Id).Should().BeEquivalentTo([scenario.OnOpen.Id, scenario.OnPrivate.Id]);

        onRequest!.Outcomes.Should().HaveCount(2);
        onRequest.Outcomes.Single(o => o.TargetId == projectId).Project!.Name.Should().Be("Atlas");
        onRequest.Outcomes.Single(o => o.TargetId == deletedProjectId).Project.Should().BeNull();
        onRequest.Outcomes.Should().AllSatisfy(o => o.AddedBy.Id.Should().Be(scenario.Agent.Id));
    }

    private sealed record Source(Guid Id, ProjectKey Key, string Name, string Description = "desc") : ISimpleProject;

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
        var channel = IntakeChannel.Create(UniqueName("Channel"), null, 1, EventActor.System, Now);
        context.IntakeDesks.AddRange(open, summaryOnly, @private);
        context.IntakeChannels.Add(channel);
        await context.SaveChangesAsync(ct);

        var needBy = IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.Commitment, "Steering committee",
            agent.Id, new LocalDate(2026, 9, 3)).Value;
        var origin = IntakeRequestOrigin.Create("ServicePortal", UniqueName("REQ"), null).Value;

        IntakeRequest Request(IntakeDesk desk, IntakeRequestNeedBy? withNeedBy = null, IntakeRequestOrigin? withOrigin = null) =>
            IntakeRequest.Create(UniqueName("Ask"), "What is being asked for", null, requester.Id, requester.Id, desk, channel,
                withNeedBy, withOrigin, IntakeStatuses.Submitted(), EventActor.System, Now).Value;

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
