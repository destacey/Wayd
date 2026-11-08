using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Proves the Close-Played-Out-Dependencies migration stores the health <c>WorkItemDependency.CalculateHealth</c>
/// gives, and that its Down restores the previous rule's.
/// </summary>
/// <remarks>The SQL compares stored ints, so only a real SQL Server shows it matches the domain's enums.</remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class ClosePlayedOutDependenciesMigrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A case the rule distinguishes: the health the domain gives it, and the one the previous rule did.</summary>
    private sealed record Case(
        StatusCategory Predecessor,
        StatusCategory Successor,
        bool Deleted,
        DependencyPlanningHealth Current,
        DependencyPlanningHealth Previous);

    private static readonly Case[] Cases =
    [
        new(StatusCategory.Done, StatusCategory.Proposed, false, DependencyPlanningHealth.Closed, DependencyPlanningHealth.Healthy),
        new(StatusCategory.Removed, StatusCategory.Done, false, DependencyPlanningHealth.Closed, DependencyPlanningHealth.Unhealthy),
        new(StatusCategory.Removed, StatusCategory.Removed, false, DependencyPlanningHealth.Closed, DependencyPlanningHealth.Unhealthy),
        new(StatusCategory.Removed, StatusCategory.Active, false, DependencyPlanningHealth.Unhealthy, DependencyPlanningHealth.Unhealthy),
        new(StatusCategory.Active, StatusCategory.Done, false, DependencyPlanningHealth.Unhealthy, DependencyPlanningHealth.Unhealthy),
        new(StatusCategory.Done, StatusCategory.Done, true, DependencyPlanningHealth.Unknown, DependencyPlanningHealth.Unknown),
    ];

    [Fact]
    public async Task Up_StoresTheHealthTheDomainGivesARowThePreviousRuleWrote()
    {
        // Arrange — seeded through the domain, then put back to what the previous rule stored.
        var seeded = await SeedEveryCase();
        await Run(ClosePlayedOutDependencies.DownSql);

        // Act
        await Run(ClosePlayedOutDependencies.UpSql);

        // Assert
        (await HealthBySource()).Should().BeEquivalentTo(seeded.ToDictionary(s => s.Key, s => s.Value.Current));
    }

    [Fact]
    public async Task Down_RestoresThePreviousRule()
    {
        // Arrange
        var seeded = await SeedEveryCase();

        // Act
        await Run(ClosePlayedOutDependencies.DownSql);

        // Assert
        (await HealthBySource()).Should().BeEquivalentTo(seeded.ToDictionary(s => s.Key, s => s.Value.Previous));
    }

    [Fact]
    public async Task Up_RunTwice_ChangesNothingTheSecondTime()
    {
        // Arrange
        var seeded = await SeedEveryCase();
        await Run(ClosePlayedOutDependencies.UpSql);

        // Act
        var changed = await Run(ClosePlayedOutDependencies.UpSql);

        // Assert
        changed.Should().Be(0);
        (await HealthBySource()).Should().BeEquivalentTo(seeded.ToDictionary(s => s.Key, s => s.Value.Current));
    }

    /// <summary>One dependency per <see cref="Cases"/> entry, created through the domain, keyed by its predecessor.</summary>
    private async Task<Dictionary<Guid, Case>> SeedEveryCase()
    {
        await _seed.Reset(Ct);
        var (team, _) = await _seed.AddTeam(Ct);

        var seeded = new Dictionary<Guid, Case>();
        foreach (var c in Cases)
        {
            var predecessor = await _seed.AddItem(team, Ct, c.Predecessor);
            var successor = await _seed.AddItem(team, Ct, c.Successor);
            await _seed.AddDependency(predecessor.Id, successor.Id, Ct, removed: c.Deleted);
            seeded.Add(predecessor.Id, c);
        }

        (await HealthBySource()).Should().BeEquivalentTo(
            seeded.ToDictionary(s => s.Key, s => s.Value.Current), "the migration must agree with the domain");
        return seeded;
    }

    private async Task<Dictionary<Guid, DependencyPlanningHealth>> HealthBySource()
    {
        await using var context = _fixture.CreateContext();
        return await context.WorkItemDependencies
            .ToDictionaryAsync(d => d.SourceId, d => d.Health, Ct);
    }

    private async Task<int> Run(string sql)
    {
        await using var context = _fixture.CreateContext();
        return await context.Database.ExecuteSqlRawAsync(sql, Ct);
    }
}
