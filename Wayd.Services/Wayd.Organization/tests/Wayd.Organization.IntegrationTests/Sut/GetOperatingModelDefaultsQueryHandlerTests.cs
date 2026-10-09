using Moq;
using NodaTime;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The operating model defaults query against a real SQL Server container.
/// </summary>
/// <remarks>
/// The handler reaches the parent's operating models through a membership's navigation to the team of teams,
/// filtering both on complex-typed date ranges. Only a real provider shows that translates, and it is the
/// first read of the team of teams operating model table.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetOperatingModelDefaultsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate ParentMove = new(2024, 7, 1);
    private static readonly LocalDate JoinDate = new(2024, 3, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_SuggestsTheParentsZoneInEffectOnTheStartDate()
    {
        // Arrange — CARDS joins ART on JoinDate; ART moves from New York to Chicago on ParentMove
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var now = SqlServerDbContextFixture.FixedNow;
            var actor = EventActor.System;

            var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, "America/New_York", actor, now);
            art.SetOperatingModel(ParentMove, "America/Chicago", actor, now).IsSuccess.Should().BeTrue();
            var team = Team.Create("Cards", new TeamCode("CARDS"), null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);

            await seedContext.TeamOfTeams.AddAsync(art, cancellationToken);
            await seedContext.Teams.AddAsync(team, cancellationToken);
            team.AddTeamMembership(art, new MembershipDateRange(JoinDate, null), actor, now).IsSuccess.Should().BeTrue();

            await seedContext.SaveChangesAsync(cancellationToken);
            teamId = team.Id;
        }

        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings
            .Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultTimeZone = "Europe/London", DefaultCommitmentGraceDays = 2 });

        await using var context = _fixture.CreateContext();
        var handler = new GetOperatingModelDefaultsQueryHandler(context, schedulingSettings.Object);

        // Act
        var beforeJoining = await handler.Handle(new GetOperatingModelDefaultsQuery(teamId, JoinDate.PlusDays(-1)), cancellationToken);
        var beforeTheMove = await handler.Handle(new GetOperatingModelDefaultsQuery(teamId, ParentMove.PlusDays(-1)), cancellationToken);
        var afterTheMove = await handler.Handle(new GetOperatingModelDefaultsQuery(teamId, ParentMove), cancellationToken);

        // Assert
        beforeJoining!.TimeZone.Should().Be("Europe/London");
        beforeJoining.TimeZoneSource.Should().BeNull();
        beforeTheMove!.TimeZone.Should().Be("America/New_York");
        beforeTheMove.TimeZoneSource.Should().Be("Payments ART");
        afterTheMove!.TimeZone.Should().Be("America/Chicago");
        afterTheMove.CommitmentGraceDays.Should().Be(2);
    }
}
