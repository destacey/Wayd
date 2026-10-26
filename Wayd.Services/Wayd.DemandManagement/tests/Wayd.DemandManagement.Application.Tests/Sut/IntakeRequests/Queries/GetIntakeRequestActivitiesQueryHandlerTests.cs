using FluentAssertions;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestActivitiesQueryHandlerTests
{
    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _principal = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private IReadOnlyCollection<string>? _requestedEventTypes;

    private static readonly ActivityLogDto CreatedEntry = new()
    {
        EventType = nameof(IntakeRequestCreatedEvent),
        DomainArea = "DemandManagement",
        AggregateType = nameof(IntakeRequest),
        Employee = new() { Id = Guid.NewGuid(), Key = 7, Name = "Alice Submitter" },
        ClientId = "claude",
        Payload = """{"id":"x","key":1,"title":"Ask","actor":{"kind":"user","userId":"u-1","employeeId":"e-1"}}""",
    };

    public GetIntakeRequestActivitiesQueryHandlerTests()
    {
        _activityLogReader
            .Setup(r => r.ReadEventTypes(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, IReadOnlyCollection<string>, PageRequest?, CancellationToken>((_, _, types, _, _) => _requestedEventTypes = types)
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([CreatedEntry], 1, 1, 50));
    }

    private GetIntakeRequestActivitiesQueryHandler CreateHandler() =>
        new(_dbContext, _principal.Object, _activityLogReader.Object);

    private void SignedInAs(Guid employeeId, bool holdsView = true)
    {
        _principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        _principal.Setup(p => p.HasPermission(IntakeRequestAuthorization.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(holdsView);
        _principal.Setup(p => p.HoldsPermission(IntakeRequestAuthorization.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(holdsView);
    }

    private Guid ActiveEmployee()
    {
        var employee = Employee.Create(new PersonName("Desk", null, "Agent"), Guid.NewGuid().ToString("N")[..12], default,
            new EmailAddress(Guid.NewGuid().ToString("N")[..12] + "@acme.example"), null, null, null, null, true, null,
            EventActor.System, default);
        _dbContext.AddEmployees(employee);
        return employee.Id;
    }

    private IntakeRequest RequestOn(IntakeDeskVisibility visibility, params Guid[] agentIds)
    {
        var desk = new IntakeDeskFaker().WithVisibility(visibility).WithAgents(agentIds).Generate();
        var request = new IntakeRequestFaker().WithDeskId(desk.Id).Generate();
        _dbContext.AddDesks(desk);
        _dbContext.AddRequests(request);

        return request;
    }

    [Fact]
    public async Task Handle_ForASummaryReader_ReadsOnlySummaryEvents()
    {
        // Arrange
        var request = RequestOn(IntakeDeskVisibility.SummaryOnly);
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _requestedEventTypes.Should().Contain([nameof(IntakeRequestCreatedEvent), nameof(IntakeRequestRetitledEvent)])
            .And.NotContain([nameof(IntakeRequestDetailsUpdatedEvent), nameof(IntakeRequestParticipantAddedEvent)]);
    }

    [Fact]
    public async Task Handle_ForASummaryReader_ReturnsEntriesWithoutTheirActor()
    {
        // Arrange — the actor of a creation can be the submitter, who is substance
        var request = RequestOn(IntakeDeskVisibility.SummaryOnly);
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        var entry = result!.Items.Should().ContainSingle().Subject;
        entry.Employee.Should().BeNull();
        entry.ClientId.Should().BeNull();
        entry.Payload.Should().NotContain("actor").And.Contain("\"title\":\"Ask\"");
    }

    [Fact]
    public async Task Handle_ForASubstanceReader_KeepsTheActor()
    {
        // Arrange
        var request = RequestOn(IntakeDeskVisibility.Open);
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        var entry = result!.Items.Should().ContainSingle().Subject;
        entry.Employee.Should().NotBeNull();
        entry.Payload.Should().Contain("actor");
    }

    [Fact]
    public async Task Handle_ForTheRequester_ReadsSummaryAndSubstanceEvents()
    {
        // Arrange — the requester needs no claim to follow their own request
        var requesterId = Guid.NewGuid();
        var desk = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.SummaryOnly).Generate();
        var request = new IntakeRequestFaker().WithDeskId(desk.Id).WithRequesterId(requesterId).Generate();
        _dbContext.AddDesks(desk);
        _dbContext.AddRequests(request);
        SignedInAs(requesterId, holdsView: false);

        // Act
        await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        _requestedEventTypes.Should().Contain([nameof(IntakeRequestRetitledEvent), nameof(IntakeRequestDetailsUpdatedEvent)]);
    }

    [Fact]
    public async Task Handle_ForSomeoneWhoCannotSeeTheRequest_ReturnsNullAndReadsNothing()
    {
        // Arrange
        var request = RequestOn(IntakeDeskVisibility.Private);
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _activityLogReader.Verify(r => r.ReadEventTypes(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAnAgentOnTheDesk_ReadsTheRequestsOwnHistoryWithoutAnyClaim()
    {
        // Arrange
        var agentId = ActiveEmployee();
        var request = RequestOn(IntakeDeskVisibility.Private, agentId);
        SignedInAs(agentId, holdsView: false);

        // Act
        await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Id), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        _activityLogReader.Verify(r => r.ReadEventTypes(request.Id, nameof(IntakeRequest), It.IsAny<IReadOnlyCollection<string>>(),
            new PageRequest(), It.IsAny<CancellationToken>()), Times.Once);
        _requestedEventTypes.Should().Contain(nameof(IntakeRequestDetailsUpdatedEvent));
    }

    private void HistoryReturns(params ActivityLogDto[] entries) =>
        _activityLogReader
            .Setup(r => r.ReadEventTypes(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([.. entries], entries.Length, 1, 50));

    private static ActivityLogDto MarkedDuplicateOf(Guid survivorId) => new()
    {
        EventType = nameof(IntakeRequestStatusChangedEvent),
        DomainArea = "DemandManagement",
        AggregateType = nameof(IntakeRequest),
        Payload = $$$"""{"id":"x","key":1,"fromDuplicateOfId":null,"toDuplicateOfId":"{{{survivorId}}}","actor":{"kind":"user"}}""",
    };

    [Fact]
    public async Task Handle_LeavesOutASurvivingRequestTheReaderCannotSee()
    {
        // Arrange — the survivor is on a private desk; its id alone would say that it exists
        var survivor = RequestOn(IntakeDeskVisibility.Private);
        var request = RequestOn(IntakeDeskVisibility.Open);
        HistoryReturns(MarkedDuplicateOf(survivor.Id));
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        var payload = result!.Items.Single().Payload;
        payload.Should().NotContain(survivor.Id.ToString());
        payload.Should().Contain("\"toDuplicateOfId\":null");
    }

    [Fact]
    public async Task Handle_KeepsASurvivingRequestTheReaderCanSee()
    {
        // Arrange
        var survivor = RequestOn(IntakeDeskVisibility.Open);
        var request = RequestOn(IntakeDeskVisibility.Open);
        HistoryReturns(MarkedDuplicateOf(survivor.Id));
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result!.Items.Single().Payload.Should().Contain(survivor.Id.ToString());
    }

    [Fact]
    public async Task Handle_ForASummaryReader_LeavesOutTheReasonForAWorkflowSwitch()
    {
        // Arrange
        var request = RequestOn(IntakeDeskVisibility.SummaryOnly);
        HistoryReturns(new ActivityLogDto
        {
            EventType = "StatusWorkflowSwitchedEvent",
            DomainArea = "StatusWorkflows",
            AggregateType = nameof(IntakeRequest),
            Payload = """{"recordId":"x","reason":"Moved with the desk","actor":{"kind":"user"}}""",
        });
        SignedInAs(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestActivitiesQuery(new IdOrKey(request.Key), new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result!.Items.Single().Payload.Should().NotContain("Moved with the desk").And.NotContain("actor");
    }
}
