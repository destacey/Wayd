using Wayd.Common.Domain.Authorization;
using Moq;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Models.Scoring;
using Wayd.DemandManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.DemandManagement.Application.Tests.Infrastructure;

/// <summary>
/// The arrangement intake command tests share: an in-memory context, a signed-in caller with chosen
/// permissions, and builders for the employees, desks, channels and requests a test needs.
/// </summary>
public sealed class IntakeScenario
{
    private readonly HashSet<string> _permissions = [];
    private bool _readOnly;

    public IntakeScenario()
    {
        Principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) =>
                _permissions.Contains(permission) && (!_readOnly || TokenScope.Read.Permits(permission)));
        Principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => _permissions.Contains(permission));
        Principal.Setup(p => p.IsReadOnly).Returns(() => _readOnly);
        User.Setup(u => u.GetUserId()).Returns("user-1");
        Statuses.Setup(r => r.ForScope(DemandManagementWorkflowOwners.IntakeRequest.Key, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(Workflow));
        Statuses.Setup(r => r.ForAlias(DemandManagementWorkflowOwners.IntakeRequest.Key, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Guid? _, int alias, CancellationToken _) =>
                Workflow.StatusFor(alias) is { } status
                    ? Result.Success(Workflow.TargetRef(status.Id)!)
                    : Result.Failure<StatusRef>($"'{Workflow.Name}' has no status for {alias}."));
    }

    public FakeDemandManagementDbContext DbContext { get; } = new();
    public Mock<ICurrentPrincipal> Principal { get; } = new();
    public Mock<ICurrentUser> User { get; } = new();

    /// <summary>Resolves statuses from the workflow governing a desk: <see cref="Workflow"/>, for every desk.</summary>
    public Mock<IStatusResolver> Statuses { get; } = new();

    /// <summary>The workflow every desk follows, shaped like the seeded default.</summary>
    public StatusWorkflow Workflow { get; } = IntakeWorkflowFactory.Default();

    /// <summary>The status meaning Submitted in <see cref="Workflow"/>.</summary>
    public StatusRef SubmittedStatus => Workflow.Ref("Submitted");
    public TestingDateTimeProvider Clock { get; } = new(new FakeClock(Instant.FromUtc(2026, 10, 1, 8, 0, 0)));

    /// <summary>Signs in as the employee, holding the given permissions.</summary>
    public IntakeScenario SignedInAs(Employee employee, params string[] permissions)
    {
        Principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employee.Id);
        _permissions.Clear();
        _permissions.UnionWith(permissions);
        return this;
    }

    /// <summary>Has the caller use a read-only token, which narrows what they may do but not what they see.</summary>
    public IntakeScenario WithReadOnlyToken()
    {
        _readOnly = true;
        return this;
    }

    /// <summary>Browses requests on desks the caller is not on.</summary>
    public static string[] View => [IntakeRequestAuthorization.ViewPermission];

    /// <summary>The Demand Management Admin.</summary>
    public static string[] Administer => [IntakeRequestAuthorization.AdministerPermission];

    public Employee Employee(bool isActive = true)
    {
        var employee = Wayd.Common.Domain.Employees.Employee.Create(
            new PersonName("Intake", null, "Probe"),
            Guid.NewGuid().ToString("N")[..12],
            Clock.Now,
            new EmailAddress(Guid.NewGuid().ToString("N")[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: isActive,
            employeeType: null,
            EventActor.System,
            Clock.Now);
        DbContext.AddEmployees(employee);
        return employee;
    }

    public IntakeDesk Desk(IntakeDeskVisibility visibility = IntakeDeskVisibility.Open, bool isRequesterSelectable = true,
        bool requesterMayAddParticipants = true, params Guid[] agentIds) =>
        AddDesk(visibility, isRequesterSelectable, requesterMayAddParticipants,
            [.. agentIds.Select(id => new IntakeDeskMemberDetails(id, IntakeDeskRole.Agent))]);

    /// <summary>A desk on which the employees are Desk Admins.</summary>
    public IntakeDesk DeskAdministeredBy(params Guid[] adminIds) =>
        AddDesk(IntakeDeskVisibility.Open, true, true, [.. adminIds.Select(id => new IntakeDeskMemberDetails(id, IntakeDeskRole.Admin))]);

    /// <summary>An open desk with the given members in their roles.</summary>
    public IntakeDesk DeskWithMembers(params IntakeDeskMemberDetails[] members) =>
        AddDesk(IntakeDeskVisibility.Open, true, true, members);

    private IntakeDesk AddDesk(IntakeDeskVisibility visibility, bool isRequesterSelectable, bool requesterMayAddParticipants,
        IntakeDeskMemberDetails[] members)
    {
        var desk = new IntakeDeskFaker()
            .WithVisibility(visibility)
            .WithIsRequesterSelectable(isRequesterSelectable)
            .WithRequesterMayAddParticipants(requesterMayAddParticipants)
            .WithMembers(members)
            .Generate();
        DbContext.AddDesks(desk);
        return desk;
    }

    /// <summary>
    /// An active model with two numeric criteria, Business Value and Job Size, scoring <c>BV / JS</c> through a
    /// Cost of Delay output of <c>BV * 2</c>.
    /// </summary>
    public ScoringModel ScoringModel()
    {
        var model = new ScoringModelFaker().AsActiveWith(
            scales: [],
            criteria: [("Business Value", "BV", null, null), ("Job Size", "JS", null, null)],
            outputs: [("Cost of Delay", "CoD", "BV * 2", false), ("Score", "Score", "BV / JS", true)]);
        DbContext.AddScoringModels(model);
        return model;
    }

    /// <summary>Has the desk score its requests with the model, as already saved, raising nothing.</summary>
    public IntakeDesk ScoresWith(IntakeDesk desk, ScoringModel? model)
    {
        var current = DbContext.ScoringModels.SingleOrDefault(m => m.Id == desk.ScoringModelId);
        desk.ChangeScoringModel(model, current, EventActor.System, Instant.MinValue);
        desk.ClearDomainEvents();
        return desk;
    }

    /// <summary>The criterion ratings for <see cref="ScoringModel"/>, as an agent enters them.</summary>
    public static IReadOnlyList<IntakeRequestCriterionRatingInput> Ratings(ScoringModel model, decimal businessValue, decimal jobSize) =>
        [.. model.Criteria.Select(c => new IntakeRequestCriterionRatingInput(c.Id, c.Token == "BV" ? businessValue : jobSize, null))];

    /// <summary>Scores the request with its desk's model as already saved, raising nothing.</summary>
    public IntakeRequestScore Scored(IntakeRequest request, Employee scorer, decimal businessValue, decimal jobSize)
    {
        var desk = DbContext.IntakeDesks.Single(d => d.Id == request.DeskId);
        var model = DbContext.ScoringModels.Single(m => m.Id == desk.ScoringModelId);
        var values = model.Criteria.ToDictionary(c => c.Id, c => c.Token == "BV" ? businessValue : jobSize);
        var score = request.RecordScore(desk, model, values, null,
            new IntakeRequestEditor(scorer.Id, IsDeskAgent: true, EventActor.System), Clock.Now).Value;
        request.ClearDomainEvents();
        return score;
    }

    public IntakeChannel Channel()
    {
        var channel = new IntakeChannelFaker().Generate();
        DbContext.AddChannels(channel);
        return channel;
    }

    /// <summary>A company tag axis with active tags of the names.</summary>
    public IntakeRequestTagCategory TagCategory(bool allowsMany = true, params string[] tagNames)
    {
        var category = new IntakeRequestTagCategoryFaker().WithAllowsMany(allowsMany).WithTags(tagNames).Generate();
        DbContext.AddTagCategories(category);
        return category;
    }

    /// <summary>Demand Management's copy of a PPM project.</summary>
    public DemandProject Project(string key = "ATLAS", string name = "Atlas")
    {
        var project = new DemandProject(SourceProject.Any(key, name), Clock.Now);
        DbContext.AddProjects(project);
        return project;
    }

    /// <summary>Records an outcome on an accepted request as already saved, raising nothing.</summary>
    public void Outcome(IntakeRequest request, Guid targetId, Employee addedBy,
        IntakeRequestOutcomeKind kind = IntakeRequestOutcomeKind.NewProject, string? note = null)
    {
        var added = request.AddOutcome(kind, targetId, note, addedBy.Id, EventActor.System, Clock.Now);
        if (added.IsFailure)
            throw new InvalidOperationException(added.Error);
        request.ClearDomainEvents();
        request.ClearPostPersistenceActions();
    }

    /// <summary>A saved free tag: one on no axis.</summary>
    public IntakeRequestTag FreeTag(string name = "follow up")
    {
        var tag = IntakeRequestTag.CreateFree(name, EventActor.System, Instant.MinValue).Value;
        tag.ClearDomainEvents();
        DbContext.AddTags(tag);
        return tag;
    }

    /// <summary>Lets the desk's agents add free tags, as already saved.</summary>
    public IntakeDesk AllowingFreeTags(IntakeDesk desk)
    {
        desk.ChangeFreeTags(true, EventActor.System, Instant.MinValue);
        desk.ClearDomainEvents();
        desk.ClearPostPersistenceActions();
        return desk;
    }

    /// <summary>Gives the request the free tag as already saved, raising nothing. Its desk must allow free tags.</summary>
    public void TaggedFree(IntakeRequest request, IntakeRequestTag tag)
    {
        var desk = DbContext.IntakeDesks.Single(d => d.Id == request.DeskId);
        var result = request.Tag(tag, null, desk, EventActor.System, Instant.MinValue);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error);
        request.ClearDomainEvents();
    }

    /// <summary>Files the request under the tag as already saved, raising nothing.</summary>
    public void Tagged(IntakeRequest request, IntakeRequestTagCategory category, string tagName)
    {
        var desk = DbContext.IntakeDesks.Single(d => d.Id == request.DeskId);
        request.Tag(category.Tags.Single(t => t.Name == tagName), category, desk, EventActor.System, Instant.MinValue);
        request.ClearDomainEvents();
    }

    public IntakeRequest Request(IntakeDesk desk, Employee requester, params Guid[] participantIds) =>
        AddRequest(RequestFaker(desk, requester).WithParticipants(participantIds));

    /// <summary>A request moved on from Submitted to a status of the category, with the alias.</summary>
    public IntakeRequest RequestIn(IntakeDesk desk, Employee requester, StatusCategory category,
        IntakeRequestStatusAlias alias = IntakeRequestStatusAlias.None) =>
        AddRequest(RequestFaker(desk, requester).InStatus(category, alias));

    /// <summary>
    /// A request still following another workflow, in an open status named Triage with no meaning, which
    /// <see cref="Workflow"/> has nothing to match by alias or name.
    /// </summary>
    public IntakeRequest RequestOnAnotherWorkflow(IntakeDesk desk, Employee requester) =>
        AddRequest(RequestFaker(desk, requester)
            .WithStatusWorkflowId(Guid.CreateVersion7())
            .WithStatusId(Guid.CreateVersion7())
            .WithStatusName("Triage")
            .InStatus(StatusCategory.Proposed));

    /// <summary>A request the submitter entered on the requester's behalf.</summary>
    public IntakeRequest RequestOnBehalf(IntakeDesk desk, Employee requester, Employee submitter) =>
        AddRequest(RequestFaker(desk, requester).WithSubmittedById(submitter.Id));

    /// <summary>A request in the named status of <see cref="Workflow"/>, moved there from Submitted.</summary>
    public IntakeRequest RequestInStatus(IntakeDesk desk, Employee requester, string statusName) =>
        AddRequest(InStatus(RequestFaker(desk, requester), statusName));

    /// <summary>A request in Accepted, recorded as having become each project.</summary>
    public IntakeRequest AcceptedRequestWithOutcomes(IntakeDesk desk, Employee requester, params Guid[] projectIds) =>
        AddRequest(InStatus(RequestFaker(desk, requester), "Accepted")
            .WithOutcomes(IntakeRequestOutcomeKind.NewProject, projectIds));

    /// <summary>A request marked a duplicate of <paramref name="survivor"/>.</summary>
    public IntakeRequest DuplicateOf(IntakeRequest survivor, IntakeDesk desk, Employee requester) =>
        AddRequest(InStatus(RequestFaker(desk, requester), "Duplicate").WithDuplicateOfId(survivor.Id).WithStatusReason("Asked for already"));

    private IntakeRequestFaker InStatus(IntakeRequestFaker faker, string statusName)
    {
        var status = Workflow.Status(statusName);
        return faker
            .WithStatusId(status.Id)
            .WithStatusCategory(status.Category)
            .WithStatusAlias((IntakeRequestStatusAlias)status.Alias)
            .WithStatusTransitionCount(2);
    }

    private IntakeRequestFaker RequestFaker(IntakeDesk desk, Employee requester) =>
        new IntakeRequestFaker()
            .WithDeskId(desk.Id)
            .WithChannelId(Channel().Id)
            .WithRequesterId(requester.Id)
            .WithSubmittedById(requester.Id)
            .WithStatusWorkflowId(Workflow.Id)
            .WithStatusId(Workflow.Status("Submitted").Id);

    /// <summary>
    /// Records that the request moved into the named status of <see cref="Workflow"/> at the instant, as the next
    /// entry in its history. The request's own status fields are not changed.
    /// </summary>
    public void RecordMove(IntakeRequest request, string statusName, Instant changedOn)
    {
        var sequence = DbContext.IntakeRequestStatusTransitions.Count(t => t.RecordId == request.Id) + 1;
        DbContext.AddStatusTransitions(new StatusTransition(DemandManagementWorkflowOwners.IntakeRequest.Key, request.Id,
            from: null, Workflow.Ref(statusName), EventActor.System, changedOn, sequence));
    }

    /// <summary>
    /// Adds the request with the first entry every saved request has in its history: submitted, now. A faker
    /// sets the status fields directly and so records none; <see cref="RecordMove"/> adds later moves.
    /// </summary>
    private IntakeRequest AddRequest(IntakeRequestFaker faker)
    {
        var request = faker.Generate();
        DbContext.AddRequests(request);
        RecordMove(request, "Submitted", Clock.Now);
        return request;
    }
}
