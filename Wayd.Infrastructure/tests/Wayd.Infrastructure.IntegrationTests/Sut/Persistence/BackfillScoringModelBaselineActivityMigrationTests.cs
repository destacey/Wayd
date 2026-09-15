using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Scoring;
using Wayd.Common.Domain.Scoring;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Context;
using static Wayd.Infrastructure.IntegrationTests.Sut.Persistence.BaselineActivityAssertions;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the Backfill-Scoring-Model-Baseline-Activity migration over scoring models that predate their creation events.
/// </summary>
/// <remarks>
/// The migration writes every payload by hand in T-SQL, so the only proof that a baseline reads back exactly as the
/// serializer would have written it is a real SQL Server, real rows, and a comparison against the entry
/// <see cref="Wayd.Infrastructure.Persistence.Activities.ActivityLogEntryFactory"/> builds for the same event.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class BackfillScoringModelBaselineActivityMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string MigrationBefore = "20260914143148_Backfill-Planning-Baseline-Activity";

    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private sealed record SeededModels(Guid ActiveModelId, Guid EmptyModelId, Guid StrayEntryId)
    {
        public Guid[] All => [ActiveModelId, EmptyModelId];
    }

    [Fact]
    public async Task Up_BaselinesEveryScoringModel_ExactlyAsTheFactoryWouldRecordIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedWithoutHistory(ct);

            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            // Assert
            await using var verify = _fixture.CreateContext();
            // Other tests in the collection may have linked the fixture's account to an employee.
            var creator = await verify.Set<ApplicationUser>().AsNoTracking()
                .Where(u => u.Id == FixtureUserId)
                .Select(u => u.EmployeeId)
                .SingleOrDefaultAsync(ct);

            var activeRow = await SingleRow(verify, seeded.ActiveModelId, ct);
            activeRow.Id.Should().NotBe(seeded.StrayEntryId, "the older entry the model already had is cleared");
            var active = await LoadModel(verify, seeded.ActiveModelId, ct);
            active.Scales.Single(s => s.Name == "Impact").Levels.Should().HaveCount(3);
            active.Scales.Should().Contain(s => s.Levels.Count == 0);
            active.Criteria.Should().Contain(c => c.Weight != null && c.ScaleId == null);
            AssertBaseline(activeRow, Baseline(active,
                await SystemCreated(verify.ScoringModels, seeded.ActiveModelId, ct), creator, activeRow.Timestamp));

            var emptyRow = await SingleRow(verify, seeded.EmptyModelId, ct);
            var empty = await LoadModel(verify, seeded.EmptyModelId, ct);
            AssertBaseline(emptyRow, Baseline(empty,
                await SystemCreated(verify.ScoringModels, seeded.EmptyModelId, ct), creator, emptyRow.Timestamp));
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Up_RunAgain_WritesNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedWithoutHistory(ct);

            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            var before = await Snapshot(seeded.All, ct);
            before.Should().HaveCount(2);

            // Act
            // Sent as a plain command, as the migration sends it: ExecuteSqlRaw runs the text through
            // string.Format even with no parameters, and the payload templates are full of braces.
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.OpenConnectionAsync(ct);
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = BackfillScoringModelBaselineActivity.UpSql;
                await command.ExecuteNonQueryAsync(ct);
            }

            // Assert
            var after = await Snapshot(seeded.All, ct);
            after.Should().BeEquivalentTo(before);
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Up_LeavesAModelThatHasACreationEntry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);

            Guid modelId;
            await using (var context = _fixture.CreateContext())
            {
                var model = ScoringModel.Create($"Atlas kept model {UniqueCode(8)}", "Kept.", EventActor.System, Now);
                context.ScoringModels.Add(model);
                await context.SaveChangesAsync(ct);
                modelId = model.Id;
            }

            var before = await Snapshot([modelId], ct);
            before.Should().ContainSingle().Which.EventType.Should().Be(nameof(ScoringModelCreatedEvent));

            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            // Assert
            var after = await Snapshot([modelId], ct);
            after.Should().BeEquivalentTo(before);
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Down_RemovesTheBaselines()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedWithoutHistory(ct);

            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            var baselined = await Snapshot(seeded.All, ct);
            baselined.Should().HaveCount(2).And.OnlyContain(r => r.Category == ActivityCategory.Baseline);

            // Act
            await MigrateToBefore(ct);

            // Assert
            var after = await Snapshot(seeded.All, ct);
            after.Should().BeEmpty();
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    private async Task MigrateToBefore(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(MigrationBefore, ct);
    }

    private async Task RestoreLatest(CancellationToken ct)
    {
        // Leave the shared database at the latest migration for the tests that follow.
        await using var restore = _fixture.CreateContext();
        await restore.Database.MigrateAsync(ct);
    }

    private static Task<ScoringModel> LoadModel(WaydDbContext context, Guid id, CancellationToken ct) =>
        context.ScoringModels.AsNoTracking()
            .Include(m => m.Scales).ThenInclude(s => s.Levels)
            .Include(m => m.Criteria)
            .Include(m => m.Outputs)
            .SingleAsync(m => m.Id == id, ct);

    private static ScoringModelBaselinedEvent Baseline(ScoringModel model, Instant recordCreatedOn, Guid? recordCreatedById, Instant timestamp) =>
        new(model.Id, model.Key, model.Name, model.Description,
            [.. model.Scales.OrderBy(s => s.Order).Select(s => new ScoringScaleValues(s.Id, s.Name, s.Order,
                [.. s.Levels.OrderBy(l => l.Order).Select(l => new ScoringRatingLevelValues(l.Id, l.Label, l.Value, l.Order))]))],
            [.. model.Criteria.OrderBy(c => c.Order).Select(c => new ScoringCriterionValues(c.Id, c.Name, c.Token, c.Description, c.Weight, c.ScaleId, c.Order))],
            [.. model.Outputs.OrderBy(o => o.Order).Select(o => new ScoringOutputValues(o.Id, o.Name, o.Token, o.Formula, o.IsPrimary, o.Order))],
            recordCreatedOn,
            recordCreatedById,
            timestamp);

    /// <summary>
    /// Creates an active model with a rated and an empty scale, a scaled and a weighted criterion and two outputs, and a proposed model
    /// with nothing in it, through the domain, then removes the entries saving them wrote, as for models that existed
    /// before their events did. The active model keeps one older entry of another kind, which the migration has to
    /// clear.
    /// </summary>
    private async Task<SeededModels> SeedWithoutHistory(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();

        var suffix = UniqueCode(8);

        var active = ScoringModel.Create($"Atlas WSJF {suffix}", "The \"Atlas\" model\\", EventActor.System, Now,
            scales: [("Impact", [("High", 8.25m), ("Low", 1m)]), ("Unrated", [])],
            criteria: [("Business Value", "BV", "Value \"delivered\".", null, "Impact"), ("Job Size", "JS", null, 12.5m, null)],
            outputs: [("Cost of Delay", "CoD", "BV * 2", false), ("WSJF", "WSJF", "CoD / JS", true)]);
        var empty = ScoringModel.Create($"Atlas empty {suffix}", "Nothing yet.", EventActor.System, Now);
        context.ScoringModels.AddRange(active, empty);
        await context.SaveChangesAsync(ct);

        Succeeded(active.AddScaleLevel(active.Scales.Single(s => s.Name == "Impact").Id, "Medium", 5m, EventActor.System, Now));
        Succeeded(active.Activate(EventActor.System, Now));
        await context.SaveChangesAsync(ct);

        var seeded = new SeededModels(active.Id, empty.Id, Guid.CreateVersion7());

        var ids = seeded.All;
        await context.ActivityLogs.Where(a => ids.Contains(a.AggregateId)).ExecuteDeleteAsync(ct);

        context.ActivityLogs.Add(new ActivityLogEntry(
            seeded.StrayEntryId, nameof(ScoringModelActivatedEvent), ActivityCategory.StateChanged, "Scoring", "ScoringModel",
            seeded.ActiveModelId, EventActor.System, Instant.FromUtc(2025, 6, 1, 12, 0, 0), ordinal: 0, correlationId: null,
            "{}", "Scoring Model Activated"));
        await context.SaveChangesAsync(ct);

        return seeded;
    }

    private static string UniqueCode(int length) => Guid.NewGuid().ToString("N").ToUpperInvariant()[..length];

    private static T Succeeded<T>(CSharpFunctionalExtensions.Result<T> result)
    {
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }

    private static void Succeeded(CSharpFunctionalExtensions.Result result) =>
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

    private sealed record RowSnapshot(Guid Id, Guid AggregateId, string EventType, ActivityCategory Category, string Payload, Instant Timestamp);

    private async Task<List<RowSnapshot>> Snapshot(Guid[] aggregateIds, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await context.ActivityLogs.AsNoTracking()
            .Where(a => aggregateIds.Contains(a.AggregateId))
            .Select(a => new RowSnapshot(a.Id, a.AggregateId, a.EventType, a.Category, a.Payload, a.Timestamp))
            .ToListAsync(ct);
    }
}
