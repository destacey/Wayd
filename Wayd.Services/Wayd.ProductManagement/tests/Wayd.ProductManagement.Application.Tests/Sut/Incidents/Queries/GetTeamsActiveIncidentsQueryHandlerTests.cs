using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Queries;

public sealed class GetTeamsActiveIncidentsQueryHandlerTests : ProductCommandTestBase
{
    private readonly Guid _employeeId = Guid.CreateVersion7();
    private readonly Guid _sev1;
    private readonly Guid _sev2;
    private int _incidentKey;

    public GetTeamsActiveIncidentsQueryHandlerTests()
    {
        var set = IncidentSeveritySet.CreateSystem(
            "SEV",
            null,
            [
                ("SEV-1", (string?)IncidentSeverityLevel.DefaultColorForSortOrder(0)),
                ("SEV-2", (string?)IncidentSeverityLevel.DefaultColorForSortOrder(1)),
            ]).Value;
        DbContext.AddIncidentSeveritySet(set);
        var levels = set.Levels.OrderBy(l => l.SortOrder).ToList();
        _sev1 = levels[0].Id;
        _sev2 = levels[1].Id;
    }

    private GetTeamsActiveIncidentsQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ShouldListOnlyActiveIncidentsOnTheTeamsProducts()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);
        var search = SeedProduct("Search");
        SeedTeamLink(search, SeedTeam("Discovery", "DIS"));

        var open = Seed("Checkout down", [checkout.Id]);
        // A custom status aliased Resolved but still in an Active category: the alias alone does not end it.
        var monitoring = Seed("Checkout slow", [checkout.Id], Status("Monitoring", StatusCategory.Active, ProductStatusAlias.Resolved));
        // The seeded Recovered status is Done, so a recovered incident is no longer active.
        Seed("Checkout restored", [checkout.Id], Status("Recovered", StatusCategory.Done, ProductStatusAlias.Resolved));
        Seed("Old outage", [checkout.Id], Status("Closed", StatusCategory.Done, ProductStatusAlias.Closed));
        Seed("False alarm", [checkout.Id], Status("Canceled", StatusCategory.Removed, ProductStatusAlias.Canceled));
        Seed("Postmortem done", [checkout.Id], Status("Reviewed", StatusCategory.Done, ProductStatusAlias.None));
        Seed("Search down", [search.Id]);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamsActiveIncidentsQuery([payments.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Select(i => i.Key).Should().BeEquivalentTo([open.Key, monitoring.Key]);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutProductsThatAreNoLongerProposedOrActive()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);
        var legacyCart = SeedProduct("Legacy Cart", status: Status("Retired", StatusCategory.Done, ProductStatusAlias.None));
        SeedTeamLink(legacyCart, payments);

        var onCheckout = Seed("Checkout down", [checkout.Id]);
        var onBoth = Seed("Cart and checkout down", [legacyCart.Id, checkout.Id]);
        Seed("Legacy cart down", [legacyCart.Id]);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamsActiveIncidentsQuery([payments.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Select(i => i.Key).Should().BeEquivalentTo([onCheckout.Key, onBoth.Key]);
        result.Single(i => i.Key == onBoth.Key).Products.Select(p => p.Name).Should().Equal("Checkout");
    }

    [Fact]
    public async Task Handle_ShouldOrderMostSevereFirstThenLongestActiveThenKey()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, payments);

        var minorOld = Seed("Minor and old", [checkout.Id], noticed: Now.Minus(Duration.FromHours(5)), level: _sev2);
        var majorRecent = Seed("Major and recent", [checkout.Id], noticed: Now.Minus(Duration.FromMinutes(5)));
        var majorOld = Seed("Major and old", [checkout.Id], noticed: Now.Minus(Duration.FromHours(2)));
        var tiedFirst = Seed("Tied first", [checkout.Id], noticed: Now.Minus(Duration.FromHours(1)));
        var tiedSecond = Seed("Tied second", [checkout.Id], noticed: Now.Minus(Duration.FromHours(1)));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamsActiveIncidentsQuery([payments.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Select(i => i.Key).Should().Equal(majorOld.Key, tiedFirst.Key, tiedSecond.Key, majorRecent.Key, minorOld.Key);
        result[0].SeverityLevel.Name.Should().Be("SEV-1");
        result[^1].SeverityLevel.Name.Should().Be("SEV-2");
    }

    [Fact]
    public async Task Handle_ShouldListEachIncidentOnceWithOnlyTheTeamsProducts()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var commerce = SeedTeam("Commerce", "COM", TeamType.TeamOfTeams);
        var checkout = SeedProduct("checkout");
        SeedTeamLink(checkout, payments);
        SeedTeamLink(checkout, commerce);
        var billing = SeedProduct("Billing");
        SeedTeamLink(billing, commerce);
        var search = SeedProduct("Search");
        SeedTeamLink(search, SeedTeam("Discovery", "DIS"));

        var incident = Seed("Payments outage", [search.Id, checkout.Id, billing.Id]);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTeamsActiveIncidentsQuery([payments.Id, commerce.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        var listed = result.Should().ContainSingle().Subject;
        listed.Id.Should().Be(incident.Id);
        listed.Title.Should().Be("Payments outage");
        listed.Status.Name.Should().Be("Open");
        listed.Products.Select(p => p.Name).Should().Equal("Billing", "checkout");
    }

    [Fact]
    public async Task Handle_ForTeamsWithNoProducts_ShouldReturnNothing()
    {
        // Arrange
        var payments = SeedTeam("Payments", "PAY");
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, SeedTeam("Discovery", "DIS"));
        Seed("Checkout down", [checkout.Id]);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamsActiveIncidentsQuery([payments.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForNoTeams_ShouldReturnNothing()
    {
        // Arrange
        var checkout = SeedProduct("Checkout");
        SeedTeamLink(checkout, SeedTeam());
        Seed("Checkout down", [checkout.Id]);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetTeamsActiveIncidentsQuery([]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    private Incident Seed(
        string title,
        IReadOnlyCollection<Guid> productIds,
        StatusRef? status = null,
        Instant? noticed = null,
        Guid? level = null)
    {
        var incident = Incident.Create(
            title,
            null,
            null,
            commanderEmployeeId: null,
            level ?? _sev1,
            noticed ?? Now.Minus(Duration.FromMinutes(30)),
            null,
            null,
            productIds,
            status ?? Status("Open", StatusCategory.Active, ProductStatusAlias.Open),
            EventActor.System,
            Now,
            _employeeId,
            _employeeId).Value;
        typeof(Incident).GetProperty(nameof(Incident.Key))!.SetValue(incident, ++_incidentKey);
        DbContext.AddIncident(incident);
        return incident;
    }
}
