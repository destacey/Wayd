using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The org chart query against a real SQL Server container.
/// </summary>
/// <remarks>
/// The handler projects each team with the memberships active on the date — a filter over a complex-typed
/// date range inside a collection subquery, with NodaTime comparisons. Only a real provider shows that the
/// projection translates; the in-memory fake evaluates it as LINQ to Objects.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetFunctionalOrganizationChartQueryHandlerTests
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate MembershipStart = new(2024, 6, 1);

    private readonly SqlServerDbContextFixture _fixture;

    public GetFunctionalOrganizationChartQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_BuildsTheHierarchyFromTheMembershipsActiveOnTheDate()
    {
        // Arrange — VS ← ART ← TEAM as of today; PAST left ART before today
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var asOf = SqlServerDbContextFixture.FixedNow.InUtc().Date;

        await using (var seedContext = _fixture.CreateContext())
        {
            var now = SqlServerDbContextFixture.FixedNow;
            var actor = EventActor.System;

            var valueStream = TeamOfTeams.Create("Payments VS", new TeamCode("VS"), null, ActiveDate, actor, now);
            var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, actor, now);
            var team = Team.Create("Cards", new TeamCode("TEAM"), null, ActiveDate, Methodology.Kanban, SizingMethod.Count, actor, now);
            var past = Team.Create("Wallets", new TeamCode("PAST"), null, ActiveDate, Methodology.Kanban, SizingMethod.Count, actor, now);

            await seedContext.TeamOfTeams.AddRangeAsync([valueStream, art], cancellationToken);
            await seedContext.Teams.AddRangeAsync([team, past], cancellationToken);

            art.AddTeamMembership(valueStream, new MembershipDateRange(MembershipStart, null), now).IsSuccess.Should().BeTrue();
            team.AddTeamMembership(art, new MembershipDateRange(MembershipStart, null), now).IsSuccess.Should().BeTrue();
            past.AddTeamMembership(art, new MembershipDateRange(MembershipStart, asOf.PlusDays(-1)), now).IsSuccess.Should().BeTrue();

            await seedContext.SaveChangesAsync(cancellationToken);
        }

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Today).Returns(asOf);

        await using var context = _fixture.CreateContext();
        var handler = new GetFunctionalOrganizationChartQueryHandler(
            context, NullLogger<GetFunctionalOrganizationChartQueryHandler>.Instance, dateTimeProvider.Object);

        // Act
        var result = await handler.Handle(new GetFunctionalOrganizationChartQuery(asOf), cancellationToken);

        // Assert
        result.Total.Should().Be(4);
        result.MaxDepth.Should().Be(2);
        result.Organization.Select(u => u.Code).Should().Equal("VS", "PAST");

        var vs = result.Organization.Single(u => u.Code == "VS");
        var artUnit = vs.Children.Should().ContainSingle().Subject;
        artUnit.Path.Should().Be("VS -> ART");
        var teamUnit = artUnit.Children.Should().ContainSingle().Subject;
        teamUnit.Code.Should().Be("TEAM");
        teamUnit.Level.Should().Be(2);
        teamUnit.Path.Should().Be("VS -> ART -> TEAM");

        result.Organization.Single(u => u.Code == "PAST").Children.Should().BeNull();
    }
}
