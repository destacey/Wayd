using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Infrastructure.Persistence.Configuration;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because <see cref="ReplicaEntityBuilderExtensions.ConfigureReplicaTracking"/>
/// is a column mapping: whether the watermarks survive the JSON conversion, whether a row written before they
/// existed still reads, and whether the row version catches two writers are all things the fakes cannot see.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ReplicaEntityBuilderExtensionsTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    // Sub-second, to prove the column keeps the precision the watermark comparison depends on.
    private static readonly Instant Renamed = Instant.FromUtc(2026, 1, 15, 9, 5, 0).Plus(Duration.FromTicks(1_234_567));

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Watermarks_RoundTripThroughTheColumn()
    {
        // Arrange
        var team = await SeedTeam();

        await using (var write = new WaydDbContextAccessor(_fixture))
        {
            var tracked = await write.Context.WorkTeams.SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken);
            tracked.ApplyDetails("Borealis", new TeamCode("BOR" + team.Key % 1000), Renamed);

            // Act
            await write.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkTeams.AsNoTracking().SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken);
        saved.Watermarks.Should().Be(new TeamReplicaWatermarks(Renamed, Created));
    }

    [Fact]
    public async Task Watermarks_OnARowWrittenBeforeTheyExisted_ReadAsNothingApplied()
    {
        // Arrange — the migration's default for existing copies.
        var team = await SeedTeam();
        await using (var context = new WaydDbContextAccessor(_fixture))
        {
            await context.Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [Work].[WorkTeams] SET [Watermarks] = '{{}}' WHERE [Id] = {team.Id}",
                TestContext.Current.CancellationToken);
        }

        // Act
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkTeams.AsNoTracking().SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken);

        // Assert
        saved.Watermarks.Should().Be(TeamReplicaWatermarks.None);
    }

    [Fact]
    public async Task Version_WhenTwoWritersChangeDifferentGroupsOfOneCopy_RejectsTheSecond()
    {
        // Arrange — both load the copy, then each applies a change to a different group.
        var team = await SeedTeam();
        await using var first = new WaydDbContextAccessor(_fixture);
        await using var second = new WaydDbContextAccessor(_fixture);
        var firstCopy = await first.Context.WorkTeams.SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken);
        var secondCopy = await second.Context.WorkTeams.SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken);

        firstCopy.ApplyDetails("Borealis", new TeamCode("BOR" + team.Key % 1000), Renamed);
        await first.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        secondCopy.ApplyActivation(false, Renamed);

        // Act
        var act = () => second.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — without the version, the second save would put back the first's old Details watermark.
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task<WorkTeam> SeedTeam()
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, "Atlas", new TeamCode($"T{key}"), TeamType.Team, true), Created);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkTeams.Add(team);
        await context.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return team;
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
