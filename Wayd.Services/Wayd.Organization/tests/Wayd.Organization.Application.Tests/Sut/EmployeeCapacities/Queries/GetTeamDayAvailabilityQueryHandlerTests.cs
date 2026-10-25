using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities.Queries;

public class GetTeamDayAvailabilityQueryHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 13, 9, 30);
    private static readonly LocalDate LongBefore = new(2027, 1, 1);
    private static readonly LocalDate Monday = new(2027, 2, 1);
    private static readonly LocalDate Tuesday = new(2027, 2, 2);
    private static readonly LocalDate Wednesday = new(2027, 2, 3);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Guid _otherTeamId = Guid.NewGuid();

    public GetTeamDayAvailabilityQueryHandlerTests()
    {
        _featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.TeamCapacityManagement, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private GetTeamDayAvailabilityQueryHandler CreateHandler(Guid? defaultCalendarId = null)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = defaultCalendarId });

        return new GetTeamDayAvailabilityQueryHandler(_dbContext, settings.Object, _featureFlags.Object);
    }

    private GetTeamDayAvailabilityQuery Query() => new(_teamId, Monday, Wednesday);

    /// <summary>Someone with <paramref name="percent"/> of their time on the team from long before the range.</summary>
    private EmployeeCapacity Allocate(int percent, Guid? roleId = null, Guid? teamId = null)
    {
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.ChangeAllocations([new AllocationChange(teamId ?? _teamId, roleId, LongBefore, null, percent)], EventActor.System, Now).IsSuccess.Should().BeTrue();
        _dbContext.AddEmployeeCapacity(capacity);
        return capacity;
    }

    private HolidayCalendar SeedCalendar(params LocalDate[] holidays)
    {
        var calendar = new HolidayCalendarFaker().Generate();
        foreach (var holiday in holidays)
            calendar.AddHoliday(holiday, "Holiday", EventActor.System, Now);
        _dbContext.AddHolidayCalendar(calendar);
        return calendar;
    }

    [Fact]
    public async Task Handle_WithTheFeatureOff_ReturnsNullWhateverIsAllocated()
    {
        // Arrange
        Allocate(100).RecordTimeAway(Tuesday, Tuesday, 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now);
        _featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.TeamCapacityManagement, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var availability = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        availability.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNobodyAllocatedToTheTeam_ReturnsNull()
    {
        // Arrange
        Allocate(100, teamId: _otherTeamId);

        // Act
        var availability = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        availability.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithOnlyRolesThatDoNotCountTowardDeliveryCapacity_ReturnsNull()
    {
        // Arrange
        var lead = new TeamMemberRoleFaker().Generate();
        lead.SetCountsTowardDeliveryCapacity(false);
        _dbContext.AddTeamMemberRole(lead);
        Allocate(100, lead.Id);

        // Act
        var availability = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        availability.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenThreeOfEightShareAHolidayOnlyTheirCalendarHas_GivesFiveEighthsThatDay()
    {
        // Arrange
        var regional = SeedCalendar(Tuesday);
        for (var person = 0; person < 8; person++)
        {
            var capacity = Allocate(100);
            if (person < 3)
                capacity.ChangeHolidayCalendar(regional.Id, EventActor.System, Now);
        }

        // Act
        var availability = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        availability.Should().NotBeNull();
        availability!.Keys.Should().BeEquivalentTo([Monday, Tuesday, Wednesday]);
        availability[Monday].Should().Be(new TeamDayAvailability(8m, 8m));
        availability[Tuesday].Should().Be(new TeamDayAvailability(8m, 5m));
        availability[Tuesday].Share.Should().Be(0.625m);
    }

    [Fact]
    public async Task Handle_WhenOneOfFourIsAwayForHalfADay_GivesSevenEighthsThatDay()
    {
        // Arrange
        Allocate(100).RecordTimeAway(Tuesday, Tuesday, 50, TimeAwayReason.Training, EventActor.System, Now);
        Allocate(100);
        Allocate(100);
        Allocate(100);

        // Act
        var availability = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        availability![Tuesday].Should().Be(new TeamDayAvailability(4m, 3.5m));
        availability[Tuesday].Share.Should().Be(0.875m);
        availability[Wednesday].Share.Should().Be(1m);
    }

    [Fact]
    public async Task Handle_TakesEachPersonsHolidaysFromTheCalendarTheyFollowOrElseTheSystemDefault()
    {
        // Arrange — the system default is off on the Monday; one of two people follows a calendar off on the Tuesday
        var systemDefault = SeedCalendar(Monday);
        var regional = SeedCalendar(Tuesday);
        Allocate(100).ChangeHolidayCalendar(regional.Id, EventActor.System, Now);
        Allocate(100);

        // Act
        var availability = await CreateHandler(systemDefault.Id).Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        availability!.Select(d => (d.Key, d.Value.Share)).Should().BeEquivalentTo([(Monday, 0.5m), (Tuesday, 0.5m), (Wednesday, 1m)]);
    }

    [Fact]
    public async Task Handle_LeavesOutRolesThatDoNotCountAndAllocationsToOtherTeams()
    {
        // Arrange — the lead and someone on another team are both away; neither is this team's delivery capacity
        var lead = new TeamMemberRoleFaker().Generate();
        lead.SetCountsTowardDeliveryCapacity(false);
        var engineer = new TeamMemberRoleFaker().Generate();
        _dbContext.AddTeamMemberRoles([lead, engineer]);

        Allocate(100, lead.Id).RecordTimeAway(Tuesday, Tuesday, 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now);
        Allocate(100, teamId: _otherTeamId).RecordTimeAway(Tuesday, Tuesday, 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now);
        Allocate(60, engineer.Id);

        // Act
        var availability = await CreateHandler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        availability![Tuesday].Should().Be(new TeamDayAvailability(0.6m, 0.6m));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
