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
        var figures = await FiguresHandler().Handle(
            new GetIncidentFiguresQuery(),
            TestContext.Current.CancellationToken);
        var closedOnly = await FiguresHandler().Handle(
            new GetIncidentFiguresQuery([ProductStatusAlias.Closed]),
            TestContext.Current.CancellationToken);

        // Assert
        page.Items.Should().ContainSingle();
        page.TotalCount.Should().Be(4);
        figures.ActiveCount.Should().Be(2);
        figures.LongestActive!.Title.Should().Be("Oldest");
        figures.LongestActive.ActiveMilliseconds.Should().Be(4 * 60 * 60 * 1000);
        figures.MeanTimeToRecoverMilliseconds.Should().Be(4 * 60 * 60 * 1000);
        closedOnly.ActiveCount.Should().Be(0);
        closedOnly.LongestActive.Should().BeNull();
        closedOnly.MeanTimeToRecoverMilliseconds.Should().Be(4 * 60 * 60 * 1000);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheMeanAbsentWhenNoneAreClosed()
    {
        // Arrange
        Open("Still open", Now.Minus(Duration.FromHours(1)));
        var resolved = Open("Recovered, not closed", Now.Minus(Duration.FromHours(5)));
        resolved.MarkRecovered(
                Status("Resolved", StatusCategory.Done, ProductStatusAlias.Resolved),
                EventActor.System,
                Now,
                Now.Minus(Duration.FromHours(1)))
            .IsSuccess.Should().BeTrue();

        // Act
        var figures = await FiguresHandler().Handle(
            new GetIncidentFiguresQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        figures.ActiveCount.Should().Be(1);
        figures.LongestActive!.Title.Should().Be("Still open");
        figures.MeanTimeToRecoverMilliseconds.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldKeepTheLowerKeyWhenActiveDurationsTie()
    {
        // Arrange
        var noticed = Now.Minus(Duration.FromHours(2));
        var higherKey = Open("Higher key", noticed);
        var lowerKey = Open("Lower key", noticed);
        SetKey(higherKey, 9);
        SetKey(lowerKey, 4);

        // Act
        var figures = await FiguresHandler().Handle(
            new GetIncidentFiguresQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        figures.LongestActive!.Key.Should().Be(4);
        figures.LongestActive.Title.Should().Be("Lower key");
    }

    [Fact]
    public async Task Handle_ShouldNameTheIncidentsPeopleByDisplayName()
    {
        // Arrange
        var reporter = AddEmployee(7, new PersonName("Grace", "Brewster", "Hopper", "Jr.", "Rear Admiral"));
        var commander = AddEmployee(8, new PersonName("Alan", "Mathison", "Turing", title: "Dr."));
        var incident = Open(
            "Checkout down",
            Now.Minus(Duration.FromMinutes(5)),
            reportedBy: reporter.Id,
            commander: commander.Id);
        DateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        // Act
        var dto = await IncidentHandler().Handle(
            new GetIncidentQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        dto!.SubmittedBy.Name.Should().Be("Ada Lovelace");
        dto.ReportedBy.Id.Should().Be(reporter.Id);
        dto.ReportedBy.Key.Should().Be(7);
        dto.ReportedBy.Name.Should().Be("Grace Hopper");
        dto.Commander!.Key.Should().Be(8);
        dto.Commander.Name.Should().Be("Alan Turing");
    }

    [Fact]
    public async Task Handle_ShouldNameEachListedIncidentsPeopleByDisplayName()
    {
        // Arrange
        var reporter = AddEmployee(7, new PersonName("Grace", "Brewster", "Hopper", "Jr.", "Rear Admiral"));
        var commander = AddEmployee(8, new PersonName("Alan", "Mathison", "Turing", title: "Dr."));
        var commanded = Open(
            "Checkout down",
            Now.Minus(Duration.FromMinutes(5)),
            reportedBy: reporter.Id,
            commander: commander.Id);
        SetKey(commanded, 2);
        var unassigned = Open("Search slow", Now.Minus(Duration.FromMinutes(10)));
        SetKey(unassigned, 1);

        // Act
        var page = await ListHandler().Handle(
            new GetIncidentsQuery(new PageRequest(1, 10)),
            TestContext.Current.CancellationToken);

        // Assert
        page.Items.Select(i => i.Key).Should().Equal(2, 1);
        page.Items[0].ReportedBy.Name.Should().Be("Grace Hopper");
        page.Items[0].Commander!.Name.Should().Be("Alan Turing");
        page.Items[1].ReportedBy.Name.Should().Be("Ada Lovelace");
        page.Items[1].Commander.Should().BeNull();
    }

    private GetIncidentQueryHandler IncidentHandler() =>
        new(DbContext, _dispatcher.Object, DateTimeProvider.Object);

    private GetIncidentsQueryHandler ListHandler() => new(DbContext);

    private GetIncidentFiguresQueryHandler FiguresHandler() =>
        new(DbContext, DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldCountAndPageOnlyTheIncidentsTheFilterMatches()
    {
        // Arrange
        var reporter = AddEmployee(7, new PersonName("Grace", null, "Hopper"));
        var first = Open("Checkout down", Now.Minus(Duration.FromMinutes(30)), reportedBy: reporter.Id);
        SetKey(first, 1);
        var second = Open("Search slow", Now.Minus(Duration.FromMinutes(20)), reportedBy: reporter.Id);
        SetKey(second, 2);
        var other = Open("Login loop", Now.Minus(Duration.FromMinutes(10)));
        SetKey(other, 3);

        // Act
        var page = await ListHandler().Handle(
            new GetIncidentsQuery(new PageRequest(1, 1), Filter: $"reportedBy={reporter.Id}", Sort: "key asc"),
            TestContext.Current.CancellationToken);

        // Assert
        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle().Which.Key.Should().Be(1);
    }

    private Employee AddEmployee(int key, PersonName name)
    {
        var employee = new PrivateConstructorFaker<Employee>()
            .RuleFor(e => e.Id, Guid.CreateVersion7())
            .RuleFor(e => e.Key, key)
            .RuleFor(e => e.Name, name)
            .Generate();
        DbContext.Employees.Add(employee);
        return employee;
    }

    private Incident Open(
        string title,
        Instant noticed,
        Instant? started = null,
        Instant? recovered = null,
        Guid? reportedBy = null,
        Guid? commander = null)
    {
        var incident = Incident.Create(
            title,
            null,
            null,
            commanderEmployeeId: commander,
            _levelId,
            noticed,
            started,
            recovered,
            [],
            Status("Open", StatusCategory.Active, ProductStatusAlias.Open),
            EventActor.System,
            Now,
            _employeeId,
            reportedBy ?? _employeeId).Value;
        DbContext.AddIncident(incident);
        return incident;
    }

    private static void SetKey(Incident incident, int key) =>
        typeof(Incident).GetProperty(nameof(Incident.Key))!.SetValue(incident, key);
}
