using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.ProductManagement.Application.Incidents.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Queries;

public sealed class GetIncidentDurationsQueryHandlerTests : ProductCommandTestBase
{
    private readonly Guid _employeeId = Guid.CreateVersion7();
    private readonly Guid _levelId;
    private readonly Mock<IDispatcher> _dispatcher = new();

    public GetIncidentDurationsQueryHandlerTests()
    {
        var set = IncidentSeveritySet.CreateSystem(
            "SEV",
            null,
            [
                ("SEV-1", (string?)IncidentSeverityLevel.DefaultColorForSortOrder(0)),
                ("SEV-2", (string?)IncidentSeverityLevel.DefaultColorForSortOrder(1)),
            ]).Value;
        DbContext.AddIncidentSeveritySet(set);
        _levelId = set.Levels.First().Id;

        var employee = new PrivateConstructorFaker<Employee>()
            .RuleFor(e => e.Id, _employeeId)
            .RuleFor(e => e.Key, 4)
            .RuleFor(e => e.Name, new PersonName("Ada", null, "Lovelace"))
            .Generate();
        DbContext.Employees.Add(employee);

        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetActiveCommunicationConnectionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ActiveCommunicationConnectionSummaryDto>());
    }

    [Fact]
    public async Task Handle_ShouldMeasureTheOpenIncidentUntilNow()
    {
        // Arrange
        var noticed = Now.Minus(Duration.FromMinutes(90));
        var incident = Open("Checkout down", noticed);
        DateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        // Act
        var dto = await IncidentHandler().Handle(
            new GetIncidentQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        dto!.Durations.NoticedToResponseStartedMilliseconds.Should().BeNull();
        dto.Durations.ResponseToRecoveredMilliseconds.Should().BeNull();
        dto.Durations.ActiveMilliseconds.Should().Be(90 * 60 * 1000);
    }

    [Fact]
    public async Task Handle_ShouldNotGiveACanceledIncidentARecoveryDuration()
    {
        // Arrange
        var noticed = Now.Minus(Duration.FromHours(2));
        var started = noticed.Plus(Duration.FromMinutes(15));
        var incident = Open("False alarm", noticed, started, Now);
        incident.Cancel(
                Status("Canceled", StatusCategory.Removed, ProductStatusAlias.Canceled),
                EventActor.System,
                Now)
            .IsSuccess.Should().BeTrue();

        // Act
        var dto = await IncidentHandler().Handle(
            new GetIncidentQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        dto!.Durations.NoticedToResponseStartedMilliseconds.Should().Be(15 * 60 * 1000);
        dto.Durations.ResponseToRecoveredMilliseconds.Should().BeNull();
        dto.Durations.ActiveMilliseconds.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReportFiguresForTheFilteredSetRatherThanThePage()
    {
        // Arrange
        Open("Recent", Now.Minus(Duration.FromMinutes(10)));
        Open("Oldest", Now.Minus(Duration.FromHours(4)));
        var closed = Open("Recovered overnight", Now.Minus(Duration.FromHours(6)));
        closed.MarkRecovered(
                Status("Resolved", StatusCategory.Done, ProductStatusAlias.Resolved),
                EventActor.System,
                Now,
                Now.Minus(Duration.FromHours(2)))
            .IsSuccess.Should().BeTrue();
        closed.Close(Status("Closed", StatusCategory.Done, ProductStatusAlias.Closed), EventActor.System, Now)
            .IsSuccess.Should().BeTrue();
        var canceled = Open("Not real", Now.Minus(Duration.FromHours(8)), Now.Minus(Duration.FromHours(7)), Now);
        canceled.Cancel(
                Status("Canceled", StatusCategory.Removed, ProductStatusAlias.Canceled),
                EventActor.System,
                Now)
            .IsSuccess.Should().BeTrue();

        // Act
        var page = await ListHandler().Handle(
            new GetIncidentsQuery(new PageRequest(1, 1)),
            TestContext.Current.CancellationToken);
        var closedOnly = await ListHandler().Handle(
            new GetIncidentsQuery(new PageRequest(1, 20), [ProductStatusAlias.Closed]),
            TestContext.Current.CancellationToken);

        // Assert
        page.Items.Should().ContainSingle();
        page.TotalCount.Should().Be(4);
        page.ActiveCount.Should().Be(2);
        page.LongestActive!.Title.Should().Be("Oldest");
        page.LongestActive.ActiveMilliseconds.Should().Be(4 * 60 * 60 * 1000);
        page.MeanTimeToRecoverMilliseconds.Should().Be(4 * 60 * 60 * 1000);
        closedOnly.ActiveCount.Should().Be(0);
        closedOnly.LongestActive.Should().BeNull();
        closedOnly.MeanTimeToRecoverMilliseconds.Should().Be(4 * 60 * 60 * 1000);
    }

    private GetIncidentQueryHandler IncidentHandler() =>
        new(DbContext, _dispatcher.Object, DateTimeProvider.Object);

    private GetIncidentsQueryHandler ListHandler() =>
        new(DbContext, DateTimeProvider.Object);

    private Incident Open(string title, Instant noticed, Instant? started = null, Instant? recovered = null)
    {
        var incident = Incident.Create(
            title,
            null,
            null,
            commanderEmployeeId: null,
            _levelId,
            noticed,
            started,
            recovered,
            [],
            Status("Open", StatusCategory.Active, ProductStatusAlias.Open),
            EventActor.System,
            Now,
            _employeeId,
            _employeeId).Value;
        DbContext.AddIncident(incident);
        return incident;
    }
}
