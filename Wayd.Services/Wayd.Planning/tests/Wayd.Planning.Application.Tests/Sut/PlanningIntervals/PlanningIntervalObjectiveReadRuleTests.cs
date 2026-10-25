using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals;

public sealed class PlanningIntervalObjectiveReadRuleTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 9, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly PlanningTeam _team = new PlanningTeamFaker(TeamType.Team).Generate();

    public PlanningIntervalObjectiveReadRuleTests() => GrantView(true);

    private PlanningIntervalObjectiveReadRule CreateSut() =>
        new(_dbContext, _currentPrincipal.Object, new TestingDateTimeProvider(new FakeClock(Now)));

    private void GrantView(bool granted) =>
        _currentPrincipal
            .Setup(p => p.HasPermission("Permissions.PlanningIntervalObjectives.View", It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    private PlanningIntervalObjectiveFaker ObjectiveFaker(Guid planningIntervalId, ObjectiveStatus status = ObjectiveStatus.NotStarted) =>
        new(planningIntervalId, _team, status, false);

    private static PlanningIntervalObjective WithHealth(PlanningIntervalObjective objective, HealthStatus status, Duration expiresIn)
    {
        var healthCheck = new PlanningIntervalObjectiveHealthCheckFaker(objective.Id, Now.Minus(Duration.FromHours(1)))
            .WithStatus(status)
            .WithExpiration(Now.Plus(expiresIn))
            .Generate();
        objective.SetPrivateField("_healthChecks", new List<PlanningIntervalObjectiveHealthCheck> { healthCheck });
        return objective;
    }

    [Fact]
    public void OwnerType_ShouldBeTheRegisteredObjectiveKey()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        // Persisted on every follow of an objective, so a change here orphans them all.
        ownerType.Should().Be("planning.planning-interval-objective");
    }

    [Fact]
    public async Task Readable_ShouldBeNull_WhenTheCallerCannotViewObjectives()
    {
        // Arrange
        GrantView(false);
        _dbContext.AddPlanningIntervalObjectives(ObjectiveFaker(Guid.NewGuid()).Generate(2));

        // Act
        var readable = await CreateSut().Readable(null, TestContext.Current.CancellationToken);

        // Assert
        readable.Should().BeNull();
    }

    [Fact]
    public async Task Readable_ShouldFlagUnhealthyObjectives_AndClosedOnesIncludingMissed()
    {
        // Arrange
        var piId = Guid.NewGuid();
        var red = WithHealth(ObjectiveFaker(piId, ObjectiveStatus.InProgress).Generate(), HealthStatus.Unhealthy, Duration.FromDays(7));
        var expiredRed = WithHealth(ObjectiveFaker(piId, ObjectiveStatus.InProgress).Generate(), HealthStatus.Unhealthy, Duration.FromMinutes(-1));
        var completed = ObjectiveFaker(piId, ObjectiveStatus.Completed).Generate();
        var canceled = ObjectiveFaker(piId, ObjectiveStatus.Canceled).Generate();
        var missed = ObjectiveFaker(piId, ObjectiveStatus.Missed).Generate();
        var notStarted = ObjectiveFaker(piId).Generate();
        _dbContext.AddPlanningIntervalObjectives([red, expiredRed, completed, canceled, missed, notStarted]);

        // Act
        var readable = await CreateSut().Readable(null, TestContext.Current.CancellationToken);

        // Assert
        var facts = readable!.ToList();
        facts.Should().HaveCount(6);
        facts.Where(f => f.IsUnhealthy).Select(f => f.Id).Should().Equal(red.Id);
        facts.Where(f => f.IsClosed).Select(f => f.Id).Should().BeEquivalentTo([completed.Id, canceled.Id, missed.Id]);
    }

    [Theory]
    [InlineData("checkout")]
    [InlineData("7")]
    public async Task Readable_ShouldMatchASearchByNameOrExactKey(string search)
    {
        // Arrange
        var piId = Guid.NewGuid();
        var match = ObjectiveFaker(piId).WithKey(7).WithName("Launch checkout").Generate();
        _dbContext.AddPlanningIntervalObjectives([match, ObjectiveFaker(piId).WithKey(70).WithName("Harden search").Generate()]);

        // Act
        var readable = await CreateSut().Readable(search, TestContext.Current.CancellationToken);

        // Assert
        readable!.Select(f => f.Id).Should().Equal(match.Id);
    }

    [Theory]
    [InlineData(ObjectiveStatus.NotStarted, LifecycleCategory.NotStarted)]
    [InlineData(ObjectiveStatus.InProgress, LifecycleCategory.Active)]
    [InlineData(ObjectiveStatus.Completed, LifecycleCategory.Completed)]
    [InlineData(ObjectiveStatus.Missed, LifecycleCategory.Completed)]
    [InlineData(ObjectiveStatus.Canceled, LifecycleCategory.Canceled)]
    public void LifecycleOf_ShouldPlaceEachStatus(ObjectiveStatus status, LifecycleCategory expected)
    {
        // Act
        var lifecycle = PlanningIntervalObjectiveReadRule.LifecycleOf(status);

        // Assert
        lifecycle.Should().Be(expected);
    }

    [Fact]
    public async Task Describe_ShouldDescribeEachObjectiveUnderItsPlanningInterval_OwnedByItsTeam()
    {
        // Arrange
        var pi26 = new PlanningIntervalFaker().WithKey(26).WithName("PI 2026.1").Generate();
        _dbContext.AddPlanningIntervals([pi26]);
        var launch = WithHealth(
            ObjectiveFaker(pi26.Id, ObjectiveStatus.InProgress)
                .WithKey(7)
                .WithName("Launch checkout")
                .WithDates(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 1))
                .Generate(),
            HealthStatus.AtRisk,
            Duration.FromDays(7));
        _dbContext.AddPlanningIntervalObjectives([launch]);

        // Act
        var summaries = await CreateSut().Describe([launch.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        var summary = summaries.Should().ContainSingle().Subject.Value;
        summary.Key.Should().Be("7");
        summary.Name.Should().Be("Launch checkout");
        summary.ParentKey.Should().Be("26");
        summary.ParentName.Should().Be("PI 2026.1");
        summary.ParentId.Should().Be(pi26.Id);
        summary.StatusName.Should().Be("In Progress");
        summary.Lifecycle.Should().Be(LifecycleCategory.Active);
        summary.Health.Should().Be(HealthStatus.AtRisk);
        summary.HealthCheckId.Should().Be(launch.HealthChecks.Single().Id);
        summary.Progress.Should().Be(launch.Progress);
        summary.People.Should().Equal(new Wayd.Common.Application.Records.RecordPerson("Team", _team.Name, null));
        summary.Start.Should().Be(new LocalDate(2026, 1, 5));
        summary.End.Should().Be(new LocalDate(2026, 3, 1));
    }
}
