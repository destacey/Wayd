using FluentAssertions;
using Moq;
using Wayd.Common.Application.Records;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals;

public sealed class PlanningIntervalObjectiveReadRuleTests
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly PlanningTeam _team = new PlanningTeamFaker(TeamType.Team).Generate();

    private PlanningIntervalObjectiveReadRule CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private void GrantView(bool granted) =>
        _currentPrincipal
            .Setup(p => p.HasPermission("Permissions.PlanningIntervalObjectives.View", It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    private PlanningIntervalObjectiveFaker ObjectiveFaker(Guid planningIntervalId) =>
        new(planningIntervalId, _team, ObjectiveStatus.NotStarted, false);

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
    public async Task ReadableIds_ShouldBeEveryObjective_WhenTheCallerCanViewObjectives()
    {
        // Arrange
        GrantView(true);
        var objectives = ObjectiveFaker(Guid.NewGuid()).Generate(3);
        _dbContext.AddPlanningIntervalObjectives(objectives);

        // Act
        var readable = await CreateSut().ReadableIds(TestContext.Current.CancellationToken);

        // Assert
        readable.Should().NotBeNull();
        readable!.ToList().Should().BeEquivalentTo(objectives.Select(o => o.Id));
    }

    [Fact]
    public async Task ReadableIds_ShouldBeNull_WhenTheCallerCannotViewObjectives()
    {
        // Arrange
        GrantView(false);
        _dbContext.AddPlanningIntervalObjectives(ObjectiveFaker(Guid.NewGuid()).Generate(2));

        // Act
        var readable = await CreateSut().ReadableIds(TestContext.Current.CancellationToken);

        // Assert
        readable.Should().BeNull();
    }

    [Fact]
    public async Task Describe_ShouldNameEachObjectiveUnderItsPlanningInterval()
    {
        // Arrange
        var pi26 = new PlanningIntervalFaker().WithKey(26).WithName("PI 2026.1").Generate();
        var pi27 = new PlanningIntervalFaker().WithKey(27).WithName("PI 2026.2").Generate();
        _dbContext.AddPlanningIntervals([pi26, pi27]);
        var launch = ObjectiveFaker(pi26.Id).WithKey(7).WithName("Launch checkout").Generate();
        var harden = ObjectiveFaker(pi27.Id).WithKey(8).WithName("Harden search").Generate();
        _dbContext.AddPlanningIntervalObjectives([launch, harden]);

        // Act
        var summaries = await CreateSut().Describe([launch.Id, harden.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        summaries.Should().HaveCount(2);
        summaries[launch.Id].Should().Be(new RecordSummary("7", "Launch checkout", "26", "PI 2026.1"));
        summaries[harden.Id].Should().Be(new RecordSummary("8", "Harden search", "27", "PI 2026.2"));
    }
}
