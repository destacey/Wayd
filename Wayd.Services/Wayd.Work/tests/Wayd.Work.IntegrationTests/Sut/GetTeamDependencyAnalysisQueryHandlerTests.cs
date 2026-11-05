using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the filter compares the two ends' teams, statuses and done timestamps
/// through navigations, and the rows are projected with Mapster; the fakes translate none of it.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamDependencyAnalysisQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate From = new(2026, 7, 1);
    private static readonly LocalDate To = new(2026, 9, 21);

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Instant On(int month, int day) => Instant.FromUtc(2026, month, day, 12, 0);

    /// <param name="teamIds">The teams beneath a team of teams; none makes the root a single team.</param>
    private async Task<TeamDependencyAnalysisDto?> Analysis(Guid rootId, params Guid[] teamIds)
    {
        MapsterConfiguration.Ensure();
        var rootType = teamIds.Length == 0 ? TeamType.Team : TeamType.TeamOfTeams;
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamStructureQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamStructure(
                rootId,
                [new TeamStructureTeam(rootId, 1, "ROOT", "Root", rootType),
                 .. teamIds.Select((id, i) => new TeamStructureTeam(id, i + 2, $"T{i}", $"Team {i}", TeamType.Team))],
                [],
                []));

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamDependencyAnalysisQueryHandler(context, dispatcher.Object);

        var result = await handler.Handle(new GetTeamDependencyAnalysisQuery(rootId, From, To), Ct);

        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Handle_CountsCrossTeamDependenciesOpenDuringTheRange()
    {
        // Arrange
        await _seed.Reset(Ct);
        var (root, _) = await _seed.AddTeam(Ct, TeamType.TeamOfTeams);
        var (teamA, _) = await _seed.AddTeam(Ct);
        var (teamB, _) = await _seed.AddTeam(Ct);
        var (outside, _) = await _seed.AddTeam(Ct);
        var (otherOutside, _) = await _seed.AddTeam(Ct);

        var open = await _seed.AddItem(teamA, Ct);
        var other = await _seed.AddItem(teamA, Ct);
        var doneInRange = await _seed.AddItem(teamA, Ct, StatusCategory.Done, done: On(8, 10));
        var doneBefore = await _seed.AddItem(teamA, Ct, StatusCategory.Done, done: On(6, 15));
        var doneBeforeLinkedLater = await _seed.AddItem(teamA, Ct, StatusCategory.Done, done: On(6, 15));
        // Removed, but its successor is still open: the successor needs work nobody will do, so it stays open.
        var removedInRange = await _seed.AddItem(teamA, Ct, StatusCategory.Removed, done: On(8, 20));
        var removedBefore = await _seed.AddItem(teamA, Ct, StatusCategory.Removed, done: On(6, 10));
        var removedWithDoneSuccessor = await _seed.AddItem(teamA, Ct, StatusCategory.Removed, done: On(6, 10));
        // Done with no done timestamp: it closed when it was linked, in May, before the range.
        var doneUntimed = await _seed.AddItem(teamA, Ct, StatusCategory.Done);
        var successor = await _seed.AddItem(teamB, Ct);
        var successorRemovedBefore = await _seed.AddItem(teamB, Ct, StatusCategory.Removed, done: On(6, 12));
        var successorDoneInRange = await _seed.AddItem(teamB, Ct, StatusCategory.Done, done: On(8, 25));
        var outsider = await _seed.AddItem(outside, Ct);
        var otherOutsider = await _seed.AddItem(otherOutside, Ct);
        var noTeam = await _seed.AddItem(null, Ct);

        await _seed.AddDependency(open.Id, successor.Id, Ct, createdOn: On(8, 1));
        await _seed.AddDependency(doneInRange.Id, outsider.Id, Ct, createdOn: On(6, 1));
        await _seed.AddDependency(removedInRange.Id, noTeam.Id, Ct, createdOn: On(7, 15));
        await _seed.AddDependency(doneBefore.Id, successor.Id, Ct, createdOn: On(5, 1));
        await _seed.AddDependency(doneBeforeLinkedLater.Id, successor.Id, Ct, createdOn: On(8, 5));
        await _seed.AddDependency(doneUntimed.Id, successor.Id, Ct, createdOn: On(5, 1));
        await _seed.AddDependency(removedBefore.Id, successorRemovedBefore.Id, Ct, createdOn: On(5, 1));
        await _seed.AddDependency(removedWithDoneSuccessor.Id, successorDoneInRange.Id, Ct, createdOn: On(5, 2));
        await _seed.AddDependency(other.Id, successor.Id, Ct, createdOn: On(9, 25));    // after the range
        await _seed.AddDependency(open.Id, other.Id, Ct, createdOn: On(8, 1));          // intra-team
        await _seed.AddDependency(successor.Id, open.Id, Ct, removed: true, createdOn: On(8, 1));
        await _seed.AddDependency(outsider.Id, otherOutsider.Id, Ct, createdOn: On(8, 1)); // no end in scope

        // Act
        var analysis = await Analysis(root, teamA, teamB);

        // Assert
        analysis!.Dependencies.Select(d => (d.Source.Id, d.Target.Id, d.Health.Name == "Closed")).Should().Equal(
            (removedWithDoneSuccessor.Id, successorDoneInRange.Id, true),
            (doneInRange.Id, outsider.Id, true),
            (removedInRange.Id, noTeam.Id, false),
            (open.Id, successor.Id, false),
            (doneBeforeLinkedLater.Id, successor.Id, true));
        analysis.Summary.Should().BeEquivalentTo(new DependencyAnalysisSummaryDto
        {
            Dependencies = 5,
            Created = 3,
            Closed = 3,
            OpenAtEnd = 2,
            TeamsInvolved = 3,
            TeamsOutside = 1,
        });
    }

    [Fact]
    public async Task Handle_SingleTeam_CountsItsDependenciesWithOtherTeamsEitherWay()
    {
        // Arrange
        await _seed.Reset(Ct);
        var (team, _) = await _seed.AddTeam(Ct);
        var (partner, _) = await _seed.AddTeam(Ct);

        var ours = await _seed.AddItem(team, Ct);
        var ourOther = await _seed.AddItem(team, Ct);
        // Done with no done timestamp, linked inside the range: it closed as it was linked.
        var ourUntimed = await _seed.AddItem(team, Ct, StatusCategory.Done);
        var theirs = await _seed.AddItem(partner, Ct);

        await _seed.AddDependency(ours.Id, theirs.Id, Ct, createdOn: On(8, 1));
        await _seed.AddDependency(theirs.Id, ours.Id, Ct, createdOn: On(8, 2));
        await _seed.AddDependency(ourUntimed.Id, theirs.Id, Ct, createdOn: On(8, 3));
        await _seed.AddDependency(ours.Id, ourOther.Id, Ct, createdOn: On(8, 4));  // intra-team

        // Act
        var analysis = await Analysis(team);

        // Assert
        analysis!.Summary.Should().BeEquivalentTo(new DependencyAnalysisSummaryDto
        {
            Dependencies = 3,
            Created = 3,
            Closed = 1,
            OpenAtEnd = 2,
            TeamsInvolved = 2,
            TeamsOutside = 1,
        });
        analysis.Pairs.Select(p => (p.PredecessorTeam!.Id, p.SuccessorTeam!.Id, p.Dependencies, p.PredecessorTeamInScope, p.SuccessorTeamInScope))
            .Should().Equal((team, partner, 2, true, false), (partner, team, 1, false, true));
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Arrange
        await _seed.Reset(Ct);

        // Act
        var analysis = await Analysis(Guid.NewGuid());

        // Assert
        analysis.Should().BeNull();
    }
}
