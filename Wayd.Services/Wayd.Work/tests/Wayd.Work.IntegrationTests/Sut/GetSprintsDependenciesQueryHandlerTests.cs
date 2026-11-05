using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the filter compares the two ends' teams and sprints through
/// navigations, and the rows are projected with Mapster; the fakes never translate either.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetSprintsDependenciesQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly DependencyState[] _allStates = [DependencyState.ToDo, DependencyState.InProgress, DependencyState.Done];

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<List<DependencyDto>> Dependencies(IEnumerable<Guid> sprintIds, IEnumerable<DependencyState>? states = null)
    {
        MapsterConfiguration.Ensure();
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetSprintsDependenciesQueryHandler(accessor.Context);

        return await handler.Handle(new GetSprintsDependenciesQuery(sprintIds, states ?? _allStates), Ct);
    }

    [Fact]
    public async Task Handle_KeepsDependenciesThatCrossTeamLinesWithAnEndInTheSprints()
    {
        // Arrange
        await _seed.Reset(Ct);
        var teamA = await _seed.AddTeam(Ct);
        var teamB = await _seed.AddTeam(Ct);
        var inSprint = await _seed.AddSprint(teamA.Id, new LocalDate(2026, 9, 14), Ct);
        var laterSprint = await _seed.AddSprint(teamB.Id, new LocalDate(2026, 12, 7), Ct);

        var planned = await _seed.AddItem(teamA.Id, Ct, iterationId: inSprint);
        var plannedOtherTeam = await _seed.AddItem(teamB.Id, Ct);
        var plannedOutsideSprints = await _seed.AddItem(teamB.Id, Ct, iterationId: laterSprint);
        var noTeam = await _seed.AddItem(null, Ct);
        var plannedNoTeam = await _seed.AddItem(null, Ct, iterationId: inSprint);
        var otherNoTeam = await _seed.AddItem(null, Ct);
        var sameTeam = await _seed.AddItem(teamA.Id, Ct);
        var unplannedA = await _seed.AddItem(teamA.Id, Ct);
        var unplannedB = await _seed.AddItem(teamB.Id, Ct);

        await _seed.AddDependency(planned.Id, plannedOtherTeam.Id, Ct);       // cross-team, predecessor in a sprint
        await _seed.AddDependency(plannedOutsideSprints.Id, planned.Id, Ct);  // cross-team, successor in a sprint
        await _seed.AddDependency(planned.Id, noTeam.Id, Ct);                 // an end with no team
        await _seed.AddDependency(plannedNoTeam.Id, otherNoTeam.Id, Ct);      // both ends with no team
        await _seed.AddDependency(planned.Id, sameTeam.Id, Ct);               // intra-team
        await _seed.AddDependency(unplannedA.Id, unplannedB.Id, Ct);          // no end in the sprints
        await _seed.AddDependency(plannedOtherTeam.Id, planned.Id, Ct, removed: true);

        // Act
        var dependencies = await Dependencies([inSprint]);

        // Assert
        dependencies.Select(d => (d.Source.Id, d.Target.Id, d.Scope.Id)).Should().BeEquivalentTo([
            (planned.Id, plannedOtherTeam.Id, (int)DependencyScope.CrossTeam),
            (plannedOutsideSprints.Id, planned.Id, (int)DependencyScope.CrossTeam),
            (planned.Id, noTeam.Id, (int)DependencyScope.Unknown),
            (plannedNoTeam.Id, otherNoTeam.Id, (int)DependencyScope.Unknown)]);

        var first = dependencies.Single(d => d.Target.Id == plannedOtherTeam.Id);
        first.Source.Team!.Id.Should().Be(teamA.Id);
        first.Source.Sprint!.Id.Should().Be(inSprint);
        first.Target.Team!.Id.Should().Be(teamB.Id);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheRequestedStates()
    {
        // Arrange
        await _seed.Reset(Ct);
        var teamA = await _seed.AddTeam(Ct);
        var teamB = await _seed.AddTeam(Ct);
        var sprint = await _seed.AddSprint(teamA.Id, new LocalDate(2026, 9, 14), Ct);

        var donePredecessor = await _seed.AddItem(teamA.Id, Ct, StatusCategory.Done, iterationId: sprint);
        var openPredecessor = await _seed.AddItem(teamA.Id, Ct, StatusCategory.Active, iterationId: sprint);
        var successor = await _seed.AddItem(teamB.Id, Ct);
        await _seed.AddDependency(donePredecessor.Id, successor.Id, Ct);
        await _seed.AddDependency(openPredecessor.Id, successor.Id, Ct);

        // Act
        var open = await Dependencies([sprint], [DependencyState.ToDo, DependencyState.InProgress]);
        var all = await Dependencies([sprint]);

        // Assert
        open.Select(d => d.Source.Id).Should().Equal(openPredecessor.Id);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NoStates_ReturnsNothing()
    {
        // Arrange
        await _seed.Reset(Ct);
        var teamA = await _seed.AddTeam(Ct);
        var teamB = await _seed.AddTeam(Ct);
        var sprint = await _seed.AddSprint(teamA.Id, new LocalDate(2026, 9, 14), Ct);
        var predecessor = await _seed.AddItem(teamA.Id, Ct, iterationId: sprint);
        var successor = await _seed.AddItem(teamB.Id, Ct);
        await _seed.AddDependency(predecessor.Id, successor.Id, Ct);

        // Act
        var dependencies = await Dependencies([sprint], []);

        // Assert
        dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoSprints_ReturnsNothing()
    {
        // Act
        var dependencies = await Dependencies([]);

        // Assert
        dependencies.Should().BeEmpty();
    }
}
