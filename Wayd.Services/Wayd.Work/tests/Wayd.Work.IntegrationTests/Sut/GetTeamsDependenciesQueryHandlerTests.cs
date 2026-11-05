using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the filter compares the two ends' teams through navigations, and
/// the rows are projected with Mapster; the fakes never translate either.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsDependenciesQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly DependencyState[] _openStates = [DependencyState.ToDo, DependencyState.InProgress];

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<List<DependencyDto>> Dependencies(IEnumerable<Guid> teamIds, IEnumerable<DependencyState>? states = null)
    {
        MapsterConfiguration.Ensure();
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetTeamsDependenciesQueryHandler(accessor.Context);

        return await handler.Handle(new GetTeamsDependenciesQuery(teamIds, states ?? _openStates), Ct);
    }

    [Fact]
    public async Task Handle_KeepsDependenciesThatCrossTeamLinesWithAnEndOnTheTeams()
    {
        // Arrange
        await _seed.Reset(Ct);
        var teamA = await _seed.AddTeam(Ct);
        var teamB = await _seed.AddTeam(Ct);
        var outside = await _seed.AddTeam(Ct);
        var otherOutside = await _seed.AddTeam(Ct);

        var a1 = await _seed.AddItem(teamA.Id, Ct);
        var a2 = await _seed.AddItem(teamA.Id, Ct);
        var b1 = await _seed.AddItem(teamB.Id, Ct);
        var out1 = await _seed.AddItem(outside.Id, Ct);
        var out2 = await _seed.AddItem(otherOutside.Id, Ct);
        var noTeam = await _seed.AddItem(null, Ct);

        await _seed.AddDependency(a1.Id, b1.Id, Ct);                // between two of the teams
        await _seed.AddDependency(out1.Id, a1.Id, Ct);              // an outside team waited on
        await _seed.AddDependency(b1.Id, noTeam.Id, Ct);            // an end with no team
        await _seed.AddDependency(a1.Id, a2.Id, Ct);                // intra-team
        await _seed.AddDependency(out1.Id, out2.Id, Ct);            // no end on the teams
        await _seed.AddDependency(b1.Id, a1.Id, Ct, removed: true);

        // Act
        var dependencies = await Dependencies([teamA.Id, teamB.Id]);

        // Assert
        dependencies.Select(d => (d.Source.Id, d.Target.Id, d.Scope.Id)).Should().BeEquivalentTo([
            (a1.Id, b1.Id, (int)DependencyScope.CrossTeam),
            (out1.Id, a1.Id, (int)DependencyScope.CrossTeam),
            (b1.Id, noTeam.Id, (int)DependencyScope.Unknown)]);
        dependencies.Single(d => d.Source.Id == out1.Id).Source.Team!.Id.Should().Be(outside.Id);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheRequestedStates()
    {
        // Arrange
        await _seed.Reset(Ct);
        var teamA = await _seed.AddTeam(Ct);
        var teamB = await _seed.AddTeam(Ct);
        var donePredecessor = await _seed.AddItem(teamA.Id, Ct, StatusCategory.Done);
        var openPredecessor = await _seed.AddItem(teamA.Id, Ct, StatusCategory.Active);
        var successor = await _seed.AddItem(teamB.Id, Ct);
        await _seed.AddDependency(donePredecessor.Id, successor.Id, Ct);
        await _seed.AddDependency(openPredecessor.Id, successor.Id, Ct);

        // Act
        var dependencies = await Dependencies([teamA.Id]);

        // Assert
        dependencies.Select(d => d.Source.Id).Should().Equal(openPredecessor.Id);
    }

    [Fact]
    public async Task Handle_NoTeams_ReturnsNothing()
    {
        // Act
        var dependencies = await Dependencies([]);

        // Assert
        dependencies.Should().BeEmpty();
    }
}
