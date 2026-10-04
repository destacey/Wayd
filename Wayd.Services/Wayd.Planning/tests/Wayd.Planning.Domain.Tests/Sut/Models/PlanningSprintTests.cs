using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Domain.Tests.Sut.Models;

public class PlanningSprintTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 4, 12, 12, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void Constructor_CopiesTheSourceAndStampsEveryGroupWithAsOf()
    {
        // Arrange
        var source = new PlanningSprintFaker().Generate();

        // Act
        var sprint = new PlanningSprint(source, Created);

        // Assert
        sprint.Id.Should().Be(source.Id);
        sprint.Key.Should().Be(source.Key);
        sprint.Name.Should().Be(source.Name);
        sprint.Type.Should().Be(source.Type);
        sprint.DateRange.Should().Be(source.DateRange);
        sprint.TeamId.Should().Be(source.TeamId);
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created));
    }

    [Fact]
    public void ApplyDetails_WhenValuesChange_AppliesAndAdvancesOnlyItsWatermark()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithName("Sprint 1").WithType(IterationType.Iteration).Generate(), Created);

        // Act
        var applied = sprint.ApplyDetails("Sprint 1a", IterationType.Sprint, Later);

        // Assert
        applied.Should().BeTrue();
        sprint.Name.Should().Be("Sprint 1a");
        sprint.Type.Should().Be(IterationType.Sprint);
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created) with { Details = Later });
    }

    [Fact]
    public void ApplyDateRange_WhenTheRangeMoves_AppliesAndAdvancesOnlyItsWatermark()
    {
        // Arrange
        var before = new IterationDateRange(new LocalDate(2026, 4, 1), new LocalDate(2026, 4, 14));
        var after = new IterationDateRange(new LocalDate(2026, 4, 1), new LocalDate(2026, 4, 21));
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithDateRange(before).Generate(), Created);

        // Act
        var applied = sprint.ApplyDateRange(after, Later);

        // Assert
        applied.Should().BeTrue();
        sprint.DateRange.Should().Be(after);
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created) with { DateRange = Later });
    }

    [Fact]
    public void ApplyTeam_WhenTheTeamChanges_AppliesAndAdvancesOnlyItsWatermark()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithTeamId(Guid.NewGuid()).Generate(), Created);

        // Act
        var applied = sprint.ApplyTeam(teamId, Later);

        // Assert
        applied.Should().BeTrue();
        sprint.TeamId.Should().Be(teamId);
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created) with { Team = Later });
    }

    [Fact]
    public void ApplyTeam_WhenTheTeamIsCleared_Applies()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithTeamId(Guid.NewGuid()).Generate(), Created);

        // Act
        var applied = sprint.ApplyTeam(null, Later);

        // Assert
        applied.Should().BeTrue();
        sprint.TeamId.Should().BeNull();
    }

    [Fact]
    public void ApplyTeam_WhenNothingChangedButNewer_AdvancesTheWatermark()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithTeamId(teamId).Generate(), Created);

        // Act
        var applied = sprint.ApplyTeam(teamId, Later);

        // Assert
        applied.Should().BeTrue();
        sprint.TeamId.Should().Be(teamId);
        sprint.Watermarks.Team.Should().Be(Later);
    }

    [Fact]
    public void ApplyDetails_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithName("Sprint 2").Generate(), Created);

        // Act
        var applied = sprint.ApplyDetails("Sprint 1", sprint.Type, Earlier);

        // Assert
        applied.Should().BeFalse();
        sprint.Name.Should().Be("Sprint 2");
        sprint.Watermarks.Details.Should().Be(Created);
    }

    [Fact]
    public void ApplyDateRange_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2026, 4, 1), new LocalDate(2026, 4, 14));
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithDateRange(range).Generate(), Created);

        // Act
        var applied = sprint.ApplyDateRange(new IterationDateRange(range.Start, new LocalDate(2026, 4, 28)), Earlier);

        // Assert
        applied.Should().BeFalse();
        sprint.DateRange.Should().Be(range);
        sprint.Watermarks.DateRange.Should().Be(Created);
    }

    [Fact]
    public void ApplyTeam_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithTeamId(teamId).Generate(), Created);

        // Act
        var applied = sprint.ApplyTeam(Guid.NewGuid(), Earlier);

        // Assert
        applied.Should().BeFalse();
        sprint.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void ApplyTeam_WhenAnotherGroupTookANewerChange_StillApplies()
    {
        // Arrange — renamed at Later; a team change from Created arrives afterwards.
        var teamId = Guid.NewGuid();
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Earlier);
        sprint.ApplyDetails("Renamed", sprint.Type, Later);

        // Act
        var applied = sprint.ApplyTeam(teamId, Created);

        // Assert
        applied.Should().BeTrue();
        sprint.TeamId.Should().Be(teamId);
        sprint.Name.Should().Be("Renamed");
    }

    [Fact]
    public void ApplyDetails_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithName("Sprint 1").Generate(), Created);
        sprint.ApplyDetails("Sprint 2", sprint.Type, Later);

        // Act
        var applied = sprint.ApplyDetails("Sprint 2", sprint.Type, Later);

        // Assert
        applied.Should().BeFalse();
        sprint.Name.Should().Be("Sprint 2");
        sprint.Watermarks.Details.Should().Be(Later);
    }

    [Fact]
    public void ApplyDateRange_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var moved = new IterationDateRange(new LocalDate(2026, 4, 1), new LocalDate(2026, 4, 21));
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        sprint.ApplyDateRange(moved, Later);

        // Act
        var applied = sprint.ApplyDateRange(moved, Later);

        // Assert
        applied.Should().BeFalse();
        sprint.DateRange.Should().Be(moved);
    }

    [Fact]
    public void ApplyTeam_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        sprint.ApplyTeam(teamId, Later);

        // Act
        var applied = sprint.ApplyTeam(teamId, Later);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void ApplyDetails_WhenATieCarriesADifferentValue_Applies()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithName("Sprint 1").Generate(), Created);

        // Act
        var applied = sprint.ApplyDetails("Sprint 1a", sprint.Type, Created);

        // Assert
        applied.Should().BeTrue();
        sprint.Name.Should().Be("Sprint 1a");
        sprint.Watermarks.Details.Should().Be(Created);
    }

    [Fact]
    public void ApplyRecord_AppliesEachGroupAgainstItsOwnWatermark()
    {
        // Arrange — the team took a change at Later; a whole record from Created renames and moves the team.
        var laterTeamId = Guid.NewGuid();
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithName("Sprint 1").Generate(), Earlier);
        sprint.ApplyTeam(laterTeamId, Later);
        var record = SameSprintAs(sprint).WithName("Sprint 1a").WithTeamId(Guid.NewGuid()).Generate();

        // Act
        var applied = sprint.ApplyRecord(record, Created);

        // Assert
        applied.Should().BeTrue();
        sprint.Name.Should().Be("Sprint 1a");
        sprint.TeamId.Should().Be(laterTeamId);
        sprint.Watermarks.Should().Be(new PlanningSprintWatermarks(Created, Created, Later));
    }

    [Fact]
    public void ApplyRecord_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        var record = SameSprintAs(sprint).WithName("Renamed").Generate();
        sprint.ApplyRecord(record, Later);

        // Act
        var applied = sprint.ApplyRecord(record, Later);

        // Assert
        applied.Should().BeFalse();
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Later));
    }

    [Fact]
    public void ApplyRecord_WithADifferentSprint_Throws()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        var other = new PlanningSprintFaker().Generate();

        // Act
        var act = () => sprint.ApplyRecord(other, Later);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created));
    }

    [Fact]
    public void Resync_WhenTheCopyAlreadyMatches_KeepsItsWatermarks()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        var source = SameSprintAs(sprint).Generate();

        // Act
        var changed = sprint.Resync(source, Later);

        // Assert
        changed.Should().BeFalse();
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created));
    }

    [Fact]
    public void Resync_StampsOnlyTheGroupsThatDiffer()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        var teamId = Guid.NewGuid();
        var source = SameSprintAs(sprint).WithTeamId(teamId).Generate();

        // Act
        var changed = sprint.Resync(source, Later);

        // Assert
        changed.Should().BeTrue();
        sprint.TeamId.Should().Be(teamId);
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created) with { Team = Later });
    }

    [Fact]
    public void Resync_WhenEveryGroupDiffers_AppliesAllOfThem()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithType(IterationType.Sprint).Generate(), Created);
        var teamId = Guid.NewGuid();
        var range = new IterationDateRange(new LocalDate(2026, 5, 1), new LocalDate(2026, 5, 14));
        var source = SameSprintAs(sprint).WithName("Renamed").WithType(IterationType.Iteration)
            .WithDateRange(range).WithTeamId(teamId).Generate();

        // Act
        var changed = sprint.Resync(source, Later);

        // Assert
        changed.Should().BeTrue();
        sprint.Name.Should().Be("Renamed");
        sprint.Type.Should().Be(IterationType.Iteration);
        sprint.DateRange.Should().Be(range);
        sprint.TeamId.Should().Be(teamId);
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Later));
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsAChangeNewerThanTheRead_LeavesThatGroupAlone()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().WithName("Sprint 1").Generate(), Earlier);
        sprint.ApplyDetails("Sprint 2", sprint.Type, Later);
        var readBeforeTheRename = SameSprintAs(sprint).WithName("Sprint 1").Generate();

        // Act
        var changed = sprint.Resync(readBeforeTheRename, Created);

        // Assert
        changed.Should().BeFalse();
        sprint.Name.Should().Be("Sprint 2");
        sprint.Watermarks.Details.Should().Be(Later);
    }

    [Fact]
    public void Resync_WithADifferentSprint_Throws()
    {
        // Arrange
        var sprint = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        var other = new PlanningSprintFaker().Generate();

        // Act
        var act = () => sprint.Resync(other, Later);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    private static PlanningSprintFaker SameSprintAs(PlanningSprint sprint) =>
        new PlanningSprintFaker()
            .WithId(sprint.Id)
            .WithKey(sprint.Key)
            .WithName(sprint.Name)
            .WithType(sprint.Type)
            .WithDateRange(sprint.DateRange)
            .WithTeamId(sprint.TeamId);
}
