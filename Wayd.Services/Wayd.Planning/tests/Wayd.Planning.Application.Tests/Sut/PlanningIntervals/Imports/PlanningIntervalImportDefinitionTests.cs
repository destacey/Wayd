using CSharpFunctionalExtensions;
using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Imports;

/// <summary>
/// Importing planning intervals. The definition's own work is refusing a name that is already taken or a
/// roster naming a team Planning has not replicated yet, then generating iterations from the cadence.
/// </summary>
public sealed class PlanningIntervalImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private static readonly LocalDate Start = new(2026, 1, 5);
    private static readonly LocalDate End = new(2026, 2, 15);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly PlanningIntervalImportDefinition _definition;

    private readonly PlanningTeam _team;

    public PlanningIntervalImportDefinitionTests()
    {
        _definition = new PlanningIntervalImportDefinition(_dbContext, new ImportPayloadSerializer());

        _team = new PlanningTeamFaker(TeamType.Team).Generate();
        _dbContext.AddPlanningTeam(_team);
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportPlanningIntervalDto Row(
        string name = "PI 2026.1",
        string? description = "The first interval of the year",
        int iterationWeeks = 2,
        string? iterationPrefix = "PI26.1-",
        IReadOnlyList<Guid>? teamIds = null) =>
        new(name, description, Start, End, iterationWeeks, iterationPrefix, teamIds ?? [_team.Id]);

    private Task<Result<ImportPassResult>> Run(params ImportPlanningIntervalDto[] planningIntervals) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(),
            CreatePass,
            [.. planningIntervals.Select((p, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(p)))],
            isFinalChunk: true,
            TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomic()
    {
        // Arrange & Act & Assert — the objectives import resolves against these intervals, so a
        // half-applied file would leave that one resolving some of its rows and rejecting the rest
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.MaxRows.Should().Be(10_000);
        _definition.Passes.Single().Name.Should().Be("CreatePlanningIntervals");
    }

    [Fact]
    public async Task CreatePlanningIntervals_CreatesEveryRowInTheFile()
    {
        // Arrange & Act
        var result = await Run(Row("PI 2026.1"), Row("PI 2026.2"), Row("PI 2026.3"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.PlanningIntervals.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreatePlanningIntervals_RecordsWhatTheRowSaid()
    {
        // Arrange & Act
        var result = await Run(Row());

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var planningInterval = _dbContext.PlanningIntervals.Single();
        planningInterval.Name.Should().Be("PI 2026.1");
        planningInterval.Description.Should().Be("The first interval of the year");
        planningInterval.DateRange.Should().Be(new LocalDateRange(Start, End));
        planningInterval.ObjectivesLocked.Should().BeFalse();
        outcome.CreatedEntityId.Should().Be(planningInterval.Id);
    }

    [Fact]
    public async Task CreatePlanningIntervals_GeneratesIterationsFromTheCadence()
    {
        // Arrange — six weeks at two weeks an iteration, so the third lands on the end date and is the
        // Innovation and Planning one
        // Act
        await Run(Row(iterationWeeks: 2, iterationPrefix: "PI26.1-"));

        // Assert
        var iterations = _dbContext.PlanningIntervals.Single().Iterations.ToList();
        iterations.Select(i => i.Name).Should().Equal("PI26.1-1", "PI26.1-2", "PI26.1-3");
        iterations.Select(i => i.DateRange.Start).Should().Equal(
            new LocalDate(2026, 1, 5), new LocalDate(2026, 1, 19), new LocalDate(2026, 2, 2));
        iterations[^1].DateRange.End.Should().Be(End);
        iterations[^1].Category.Should().Be(IterationCategory.InnovationAndPlanning);
        iterations.Take(2).Should().AllSatisfy(i => i.Category.Should().Be(IterationCategory.Development));
    }

    [Fact]
    public async Task CreatePlanningIntervals_AssignsTheRosterOnTheRow()
    {
        // Arrange
        var second = new PlanningTeamFaker(TeamType.TeamOfTeams).Generate();
        _dbContext.AddPlanningTeam(second);

        // Act
        var result = await Run(Row(teamIds: [_team.Id, second.Id]));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.PlanningIntervals.Single().Teams.Select(t => t.TeamId)
            .Should().BeEquivalentTo([_team.Id, second.Id]);
    }

    [Fact]
    public async Task CreatePlanningIntervals_LeavesAnIntervalWithNoTeamsWhenTheRosterIsBlank()
    {
        // Arrange — this import only creates, so a blank roster is an interval with no teams rather than
        // one whose teams were cleared
        // Act
        var result = await Run(Row(teamIds: []));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.PlanningIntervals.Single().Teams.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePlanningIntervals_RejectsARowWhoseNameAlreadyExists()
    {
        // Arrange — a planning interval's name is unique, and rejecting the row is what keeps the roster
        // on it from replacing the one the existing interval already has
        _dbContext.AddPlanningInterval(new PlanningIntervalFaker().WithName("PI 2026.1").Generate());

        // Act
        var result = await Run(Row("PI 2026.1"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("PI 2026.1");
        _dbContext.PlanningIntervals.Should().ContainSingle();
    }

    [Fact]
    public async Task CreatePlanningIntervals_RejectsARowNamingATeamPlanningHasNotSeen()
    {
        // Arrange — Team replication from Organization is asynchronous, and PlanningIntervalTeam.TeamId is
        // a required FK, so the alternative is the runner's save FK-faulting and taking the file with it
        var unreplicated = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(teamIds: [_team.Id, unreplicated]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(unreplicated.ToString());
        _dbContext.PlanningIntervals.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePlanningIntervals_TrimsANameSurroundedByWhitespace()
    {
        // Arrange & Act
        var result = await Run(Row(" PI 2026.1 "));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.PlanningIntervals.Single().Name.Should().Be("PI 2026.1");
    }
}
