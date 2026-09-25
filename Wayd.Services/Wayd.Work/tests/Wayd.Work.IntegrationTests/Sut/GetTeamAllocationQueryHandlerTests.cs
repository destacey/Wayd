using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkTeams.Allocation;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler filters on the work type's tier through navigations and
/// coalesces the item's own project with its inherited one in the projection; the fakes translate neither.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamAllocationQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate From = new(2026, 7, 1);
    private static readonly LocalDate To = new(2026, 9, 21);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_CountsCompletedRequirementWorkByItsOwnOrInheritedProject()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        MapsterConfiguration.Ensure();
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);
        var projectId = await seeder.AddProject(cancellationToken);
        var inWindow = Instant.FromUtc(2026, 8, 3, 15, 0);

        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: inWindow, projectId: projectId);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: inWindow, parentProjectId: projectId);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: inWindow);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: inWindow, epic: true, projectId: projectId);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Removed, done: inWindow);
        await seeder.AddItem(teamId, cancellationToken, WorkStatusCategory.Done, done: Instant.FromUtc(2026, 6, 30, 23, 0));

        var portfolio = new PpmRecordReference(Guid.NewGuid(), 1, "Customer Experience");
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamStructureQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamStructure(teamId, [new TeamStructureTeam(teamId, 1, "TEAM", "Team", TeamType.Team)], [], []));
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetProjectClassificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProjectClassification(projectId, "FP1", "Forecast Project", portfolio, null, [], false)]);

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamAllocationQueryHandler(context, dispatcher.Object);
        var options = new AllocationOptions(
            AllocationDimension.Portfolio, AllocationMeasure.Count, UnestimatedHandling.Exclude, ThemeCounting.SplitEvenly);

        // Act
        var result = await handler.Handle(new GetTeamAllocationQuery(teamId, From, To, options), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var allocation = result.Value!;
        allocation.Summary.ItemsCompleted.Should().Be(3);
        allocation.Groups.Select(g => (g.Name, g.Items)).Should().Equal(("Customer Experience", 2.0), ("No project", 1.0));
    }
}
