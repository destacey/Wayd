using Moq;
using NodaTime;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetOperatingModelDefaultsQueryHandlerTests : IDisposable
{
    private static readonly LocalDate ParentStart = new(2024, 1, 1);
    private static readonly LocalDate ParentMove = new(2024, 7, 1);
    private static readonly LocalDate JoinDate = new(2024, 3, 1);
    private static readonly LocalDate LeaveDate = new(2024, 12, 31);
    private static readonly Instant Timestamp = Instant.FromUtc(2024, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<ISettings<SchedulingSettings>> _schedulingSettings = new();
    private readonly GetOperatingModelDefaultsQueryHandler _handler;
    private readonly TeamFaker _teamFaker = new();
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker = new();

    public GetOperatingModelDefaultsQueryHandlerTests()
    {
        _schedulingSettings
            .Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultTimeZone = "Europe/London", DefaultCommitmentGraceDays = 2 });

        _handler = new GetOperatingModelDefaultsQueryHandler(_dbContext, _schedulingSettings.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>
    /// A team in a team of teams from <see cref="JoinDate"/> to <see cref="LeaveDate"/>, whose parent moved
    /// from New York to Chicago on <see cref="ParentMove"/>.
    /// </summary>
    private (Team Team, TeamOfTeams Parent) TeamWithAParentThatMoved()
    {
        var parent = _teamOfTeamsFaker.WithName("Payments ART").WithActiveDate(ParentStart).Generate();
        parent.SetOperatingModel(ParentStart, "America/New_York", EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        parent.SetOperatingModel(ParentMove, "America/Chicago", EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        var team = _teamFaker.WithActiveDate(ParentStart).Generate();
        team.AddTeamMembership(parent, new MembershipDateRange(JoinDate, LeaveDate), EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        _dbContext.AddTeamOfTeams(parent);
        _dbContext.AddTeam(team);
        return (team, parent);
    }

    [Theory]
    [InlineData(2024, 3, 1, "America/New_York")]
    [InlineData(2024, 6, 30, "America/New_York")]
    [InlineData(2024, 7, 1, "America/Chicago")]
    [InlineData(2024, 12, 31, "America/Chicago")]
    public async Task Handle_WhileInATeamOfTeams_SuggestsItsZoneOnTheStartDate(int year, int month, int day, string expectedTimeZone)
    {
        // Arrange
        var (team, _) = TeamWithAParentThatMoved();

        // Act
        var result = await _handler.Handle(new GetOperatingModelDefaultsQuery(team.Id, new LocalDate(year, month, day)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.TimeZone.Should().Be(expectedTimeZone);
        result.TimeZoneSource.Should().Be("Payments ART");
        result.CommitmentGraceDays.Should().Be(2);
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 1, 1)]
    public async Task Handle_OutsideTheMembership_SuggestsTheSystemDefault(int year, int month, int day)
    {
        // Arrange
        var (team, _) = TeamWithAParentThatMoved();

        // Act
        var result = await _handler.Handle(new GetOperatingModelDefaultsQuery(team.Id, new LocalDate(year, month, day)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.TimeZone.Should().Be("Europe/London");
        result.TimeZoneSource.Should().BeNull();
        result.CommitmentGraceDays.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ForAChildTeamOfTeams_SuggestsItsParentsZone()
    {
        // Arrange
        var (_, parent) = TeamWithAParentThatMoved();
        var child = _teamOfTeamsFaker.WithName("Cards Train").WithActiveDate(ParentStart).Generate();
        child.SetOperatingModel(ParentStart, "UTC", EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        child.AddTeamMembership(parent, new MembershipDateRange(ParentStart, null), EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddTeamOfTeams(child);

        // Act
        var result = await _handler.Handle(new GetOperatingModelDefaultsQuery(child.Id, ParentMove), TestContext.Current.CancellationToken);

        // Assert
        result!.TimeZone.Should().Be("America/Chicago");
        result.TimeZoneSource.Should().Be("Payments ART");
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Arrange
        TeamWithAParentThatMoved();

        // Act
        var result = await _handler.Handle(new GetOperatingModelDefaultsQuery(Guid.NewGuid(), JoinDate), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
