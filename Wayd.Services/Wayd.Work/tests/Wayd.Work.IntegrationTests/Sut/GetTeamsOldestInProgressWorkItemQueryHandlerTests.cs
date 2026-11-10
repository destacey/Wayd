using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler filters on the work type's tier through navigations, which the
/// fakes do not translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsOldestInProgressWorkItemQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ReturnsEachTeamsEarliestActivatedRequirementInProgress()
    {
        // Arrange
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        var other = await _seed.AddTeam(Ct);
        var idle = await _seed.AddTeam(Ct);
        var day = ForecastSeeder.Now;

        await _seed.AddItem(team.Id, Ct, StatusCategory.Active, activated: day.Minus(Duration.FromDays(3)));
        var oldest = await _seed.AddItem(team.Id, Ct, StatusCategory.Active, activated: day.Minus(Duration.FromDays(9)));
        await _seed.AddItem(team.Id, Ct, StatusCategory.Active);                                                           // no start time
        await _seed.AddItem(team.Id, Ct, StatusCategory.Active, epic: true, activated: day.Minus(Duration.FromDays(40)));  // not a requirement
        await _seed.AddItem(team.Id, Ct, StatusCategory.Done, activated: day.Minus(Duration.FromDays(30)), done: day);
        var otherItem = await _seed.AddItem(other.Id, Ct, StatusCategory.Active, activated: day.Minus(Duration.FromDays(1)));
        await _seed.AddItem(idle.Id, Ct);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetTeamsOldestInProgressWorkItemQueryHandler(accessor.Context);

        // Act
        var result = await handler.Handle(new GetTeamsOldestInProgressWorkItemQuery([team.Id, other.Id, idle.Id]), Ct);

        // Assert
        result.Keys.Should().BeEquivalentTo([team.Id, other.Id]);
        result[team.Id].Id.Should().Be(oldest.Id);
        result[team.Id].Key.Should().Be(oldest.Key);
        result[team.Id].WorkspaceKey.Should().Be(ForecastSeeder.WorkspaceKey);
        result[team.Id].ActivatedOn.Should().Be(day.Minus(Duration.FromDays(9)));
        result[other.Id].Id.Should().Be(otherItem.Id);
    }
}
