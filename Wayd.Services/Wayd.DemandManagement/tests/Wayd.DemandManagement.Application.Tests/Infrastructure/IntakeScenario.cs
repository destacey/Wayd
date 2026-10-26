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
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
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
        Statuses.Setup(r => r.ForAlias(DemandManagementWorkflowOwners.IntakeRequest.Key, It.IsAny<Guid?>(),
                (int)IntakeRequestStatusAlias.Submitted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SubmittedStatus));
    }

    public FakeDemandManagementDbContext DbContext { get; } = new();
    public Mock<ICurrentPrincipal> Principal { get; } = new();
    public Mock<ICurrentUser> User { get; } = new();

    /// <summary>Resolves statuses from the workflow governing a desk. Hands out <see cref="SubmittedStatus"/> for every desk.</summary>
    public Mock<IStatusResolver> Statuses { get; } = new();

    /// <summary>The status meaning Submitted, which every desk's workflow resolves to.</summary>
    public StatusRef SubmittedStatus { get; } = IntakeStatusRefFactory.Submitted();
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

    public IntakeChannel Channel()
    {
        var channel = new IntakeChannelFaker().Generate();
        DbContext.AddChannels(channel);
        return channel;
    }

    public IntakeRequest Request(IntakeDesk desk, Employee requester, params Guid[] participantIds) =>
        AddRequest(RequestFaker(desk, requester).WithParticipants(participantIds));

    /// <summary>A request moved on from Submitted to a status of the category, with the alias.</summary>
    public IntakeRequest RequestIn(IntakeDesk desk, Employee requester, StatusCategory category,
        IntakeRequestStatusAlias alias = IntakeRequestStatusAlias.None) =>
        AddRequest(RequestFaker(desk, requester).InStatus(category, alias));

    private IntakeRequestFaker RequestFaker(IntakeDesk desk, Employee requester) =>
        new IntakeRequestFaker()
            .WithDeskId(desk.Id)
            .WithChannelId(Channel().Id)
            .WithRequesterId(requester.Id)
            .WithSubmittedById(requester.Id);

    private IntakeRequest AddRequest(IntakeRequestFaker faker)
    {
        var request = faker.Generate();
        DbContext.AddRequests(request);
        return request;
    }
}
