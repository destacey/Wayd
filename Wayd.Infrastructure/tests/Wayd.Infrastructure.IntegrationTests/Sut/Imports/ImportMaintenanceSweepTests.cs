using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Imports;

/// <summary>
/// The two import maintenance sweeps against a real SQL Server container.
/// </summary>
/// <remarks>
/// Both are pure queries over <c>Instant</c> columns, and the retention sweep reaches its rows through the
/// <c>Rows</c> navigation rather than a foreign key an in-memory fake never fills in. Neither shape can be
/// shown to translate anywhere but here — against a fake they run as LINQ to Objects and pass regardless.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ImportMaintenanceSweepTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly SqlServerDbContextFixture _fixture;

    public ImportMaintenanceSweepTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);
        return clock;
    }

    private static async Task<ImportProcess> AddRun(
        WaydDbContext context, Instant submittedOn, int rowCount, CancellationToken cancellationToken)
    {
        var rows = Enumerable.Range(1, rowCount)
            .Select(i => ImportProcessRow.Create($"r{i}", i, $$"""{"name":"Row {{i}}"}"""));

        var process = ImportProcess.Create("test-import", "user-1", null, rows, submittedOn);

        await context.ImportProcesses.AddAsync(process, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return process;
    }

    /// <summary>Drives a run to a failed terminal state at <paramref name="finishedOn"/>.</summary>
    private static void Finish(ImportProcess process, Instant finishedOn)
    {
        process.Start($"trace-{process.Id}", finishedOn);

        foreach (var row in process.Rows)
        {
            row.MarkFailed("Rejected.", finishedOn);
        }

        process.Fail("Every row was rejected.", finishedOn);
    }

    [Fact]
    public async Task RetentionSweep_ClearsPayloadsOfExpiredRunsAndLeavesRecentOnesIntact()
    {
        // Arrange — one run finished well past the window, one just inside it
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetImportData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var expired = await AddRun(context, _now - Duration.FromDays(200), 2, cancellationToken);
        var recent = await AddRun(context, _now - Duration.FromDays(40), 1, cancellationToken);

        Finish(expired, _now - Duration.FromDays(45));
        Finish(recent, _now - Duration.FromDays(10));
        await context.SaveChangesAsync(cancellationToken);

        var handler = new PurgeExpiredImportPayloadsCommandHandler(
            context, Clock().Object, NullLogger<PurgeExpiredImportPayloadsCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new PurgeExpiredImportPayloadsCommand(), cancellationToken);

        // Assert
        result.Value.Should().Be(2);

        await using var assertContext = _fixture.CreateContext();
        var saved = await assertContext.ImportProcesses
            .Include(p => p.Rows)
            .ToListAsync(cancellationToken);

        saved.Single(p => p.Id == expired.Id).Rows.Should().AllSatisfy(r => r.Payload.Should().BeNull());
        saved.Single(p => p.Id == recent.Id).Rows.Single().Payload.Should().NotBeNull();

        // Each batch is detached once saved; left tracked they would accumulate for the whole sweep, and a
        // neglected table is many batches.
        context.ChangeTracker.Entries<ImportProcessRow>().Should().BeEmpty();
    }

    [Fact]
    public async Task StallSweep_FailsARunThatStoppedReportingProgress()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetImportData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var process = await AddRun(context, _now - Duration.FromHours(3), 1, cancellationToken);
        process.Start("trace-1", _now - Duration.FromHours(3));
        await context.SaveChangesAsync(cancellationToken);

        var dispatcher = new Mock<IDispatcher>();
        var handler = new RecoverStalledImportsCommandHandler(
            context, Clock().Object, dispatcher.Object, NullLogger<RecoverStalledImportsCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new RecoverStalledImportsCommand(), cancellationToken);

        // Assert — failed and persisted, with the unapplied row left for a resume
        result.Value.Failed.Should().Be(1);

        await using var assertContext = _fixture.CreateContext();
        var saved = await assertContext.ImportProcesses
            .Include(p => p.Rows)
            .SingleAsync(p => p.Id == process.Id, cancellationToken);

        saved.Status.Should().Be(ImportProcessStatus.Failed);
        saved.Rows.Single().Status.Should().Be(ImportRowStatus.Pending);
    }

    [Fact]
    public async Task StallSweep_PublishesAgainForARunLeftQueued()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetImportData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var abandoned = await AddRun(context, _now - Duration.FromHours(2), 1, cancellationToken);
        await AddRun(context, _now - Duration.FromMinutes(1), 1, cancellationToken);

        var dispatcher = new Mock<IDispatcher>();
        var handler = new RecoverStalledImportsCommandHandler(
            context, Clock().Object, dispatcher.Object, NullLogger<RecoverStalledImportsCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new RecoverStalledImportsCommand(), cancellationToken);

        // Assert — only the one past the grace, and it stays Queued so a worker can still claim it
        result.Value.Republished.Should().Be(1);
        dispatcher.Verify(
            d => d.Publish(
                It.Is<RunImportProcessCommand>(c => c.ImportProcessId == abandoned.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        dispatcher.VerifyNoOtherCalls();
    }
}
