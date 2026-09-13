using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// The migration rewrites stored JSON with SQL Server's JSON functions, whose output has to deserialize into
/// <see cref="WorkIterationWatermarks"/> through the real column mapping. The container is already migrated, so
/// each test puts a row back into the shape it had before and runs the migration's SQL against it.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SplitWorkIterationWatermarksTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    // Sub-second, to prove the rewrite keeps the precision the watermark comparison depends on.
    private static readonly Instant Recorded = Instant.FromUtc(2026, 1, 15, 9, 5, 0).Plus(Duration.FromTicks(1_234_567));
    private static readonly Instant Renamed = Recorded.Plus(Duration.FromMinutes(5));

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Up_StampsEveryGroupWithTheRecordWatermark()
    {
        // Arrange
        var iteration = await SeedIteration();
        await SetWatermarksJson(iteration.Id, $"{{\"Record\":\"{InstantJson(Recorded)}\"}}");

        // Act
        await Run(new SplitWorkIterationWatermarks().UpOperations);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkIterations.AsNoTracking().SingleAsync(i => i.Id == iteration.Id, TestContext.Current.CancellationToken);
        saved.Watermarks.Should().Be(WorkIterationWatermarks.At(Recorded));
    }

    [Fact]
    public async Task Up_LeavesACopyWithNoWatermarkAsNothingApplied()
    {
        // Arrange
        var iteration = await SeedIteration();
        await SetWatermarksJson(iteration.Id, "{}");

        // Act
        await Run(new SplitWorkIterationWatermarks().UpOperations);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkIterations.AsNoTracking().SingleAsync(i => i.Id == iteration.Id, TestContext.Current.CancellationToken);
        saved.Watermarks.Should().Be(WorkIterationWatermarks.None);
    }

    [Fact]
    public async Task Down_KeepsTheNewestGroupAsTheRecordWatermark()
    {
        // Arrange — the details took a change after every other group.
        var iteration = await SeedIteration();
        await using (var write = new WaydDbContextAccessor(_fixture))
        {
            var tracked = await write.Context.WorkIterations.SingleAsync(i => i.Id == iteration.Id, TestContext.Current.CancellationToken);
            tracked.ApplyDetails("Renamed", tracked.Type, EventActor.System, Renamed);
            await write.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await Run(new SplitWorkIterationWatermarks().DownOperations);

        // Assert
        var json = await ReadWatermarksJson(iteration.Id);
        json.Should().Be($"{{\"Record\":\"{InstantJson(Renamed)}\"}}");
    }

    private async Task Run(IReadOnlyList<MigrationOperation> operations)
    {
        foreach (var operation in operations.Cast<SqlOperation>())
        {
            await Execute(operation.Sql);
        }
    }

    private async Task SetWatermarksJson(Guid id, string json) =>
        await Execute("UPDATE [Work].[WorkIterations] SET [Watermarks] = @json WHERE [Id] = @id", ("@json", json), ("@id", id));

    private async Task<string> ReadWatermarksJson(Guid id)
    {
        await using var context = new WaydDbContextAccessor(_fixture);
        return await context.Context.Database
            .SqlQuery<string>($"SELECT [Watermarks] AS [Value] FROM [Work].[WorkIterations] WHERE [Id] = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // Through ADO rather than ExecuteSqlRaw: the migration's SQL contains JSON braces, which EF would read as
    // format placeholders.
    private async Task Execute(string sql, params (string Name, object Value)[] parameters)
    {
        await using var context = new WaydDbContextAccessor(_fixture);
        var connection = context.Context.Database.GetDbConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<WorkIteration> SeedIteration()
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var range = new IterationDateRange(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0));
        var iteration = new WorkIteration(
            new SourceIteration(Guid.NewGuid(), key, "Sprint 1", IterationType.Sprint, IterationState.Active, range, null),
            Created);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkIterations.Add(iteration);
        await context.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return iteration;
    }

    private static string InstantJson(Instant instant) =>
        NodaTime.Text.InstantPattern.ExtendedIso.Format(instant);

    private sealed record SourceIteration(
        Guid Id, int Key, string Name, IterationType Type, IterationState State, IterationDateRange DateRange, Guid? TeamId)
        : ISimpleIteration;
}
