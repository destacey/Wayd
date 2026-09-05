using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class ResumeImportProcessCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly TestImportDefinition _definition = new(new ImportPayloadSerializer());
    private readonly Mock<ICurrentPrincipal> _principal = new();
    private readonly Mock<IDispatcher> _dispatcher = new();

    public ResumeImportProcessCommandHandlerTests()
    {
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _db.Dispose();

    private ResumeImportProcessCommandHandler CreateHandler()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);

        return new ResumeImportProcessCommandHandler(
            _db,
            new ImportDefinitionRegistry([_definition]),
            _principal.Object,
            clock.Object,
            _dispatcher.Object,
            NullLogger<ResumeImportProcessCommandHandler>.Instance);
    }

    private ImportProcess QueueRun(int rowCount)
    {
        var rows = Enumerable.Range(1, rowCount).Select(i =>
            ImportProcessRow.Create($"r{i}", i, _definition.SerializeRow(new TestImportRow($"Row {i}"))));

        var process = ImportProcess.Create(_definition.Key, "user-1", null, rows, _now);
        _db.AddImportProcess(process);
        process.Start("trace-1", _now);
        return process;
    }

    /// <summary>A part-applied run: one row succeeded, one rejected, one never reached.</summary>
    private ImportProcess PartlyAppliedRun()
    {
        var process = QueueRun(3);
        var rows = process.Rows.ToList();

        rows[0].MarkSucceeded(Guid.CreateVersion7(), _now);
        rows[1].MarkFailed("Rejected.", _now);
        rows[2].MarkCancelled(_now);

        process.RecordProgress(succeeded: 1, failed: 1, _now);
        process.Cancel(_now);

        return process;
    }

    private Task<CSharpFunctionalExtensions.Result<ResumedImport>> Resume(Guid id, bool retryFailed = false) =>
        CreateHandler().Handle(new ResumeImportProcessCommand(id, retryFailed), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_QueuesTheRunAgainAndPublishesIt()
    {
        // Arrange
        var process = PartlyAppliedRun();

        // Act
        var result = await Resume(process.Id);

        // Assert
        result.Value.QueuedRowCount.Should().Be(1);
        process.Status.Should().Be(ImportProcessStatus.Queued);
        _dispatcher.Verify(
            d => d.Publish(
                It.Is<RunImportProcessCommand>(c => c.ImportProcessId == process.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LeavesRejectedRowsAloneUnlessAskedToRetryThem()
    {
        // Arrange — a resume finishes what was never tried; it does not re-litigate what was rejected
        var process = PartlyAppliedRun();

        // Act
        await Resume(process.Id);

        // Assert
        process.Rows.Single(r => r.ImportId == "r2").Status.Should().Be(ImportRowStatus.Failed);
        process.Rows.Single(r => r.ImportId == "r3").Status.Should().Be(ImportRowStatus.Pending);
    }

    [Fact]
    public async Task Handle_WithRetryFailedRows_ReturnsRejectedRowsToTheQueue()
    {
        // Arrange
        var process = PartlyAppliedRun();

        // Act
        var result = await Resume(process.Id, retryFailed: true);

        // Assert
        result.Value.QueuedRowCount.Should().Be(2);
        process.Rows.Single(r => r.ImportId == "r2").Status.Should().Be(ImportRowStatus.Pending);
    }

    [Fact]
    public async Task Handle_NeverReturnsASucceededRowToTheQueue()
    {
        // Arrange — its payload is gone and the record it created still exists, so reapplying it duplicates
        var process = PartlyAppliedRun();

        // Act
        await Resume(process.Id, retryFailed: true);

        // Assert
        process.Rows.Single(r => r.ImportId == "r1").Status.Should().Be(ImportRowStatus.Succeeded);
    }

    [Fact]
    public async Task Handle_RecountsSoARetriedRowIsNotStillCountedAsFailed()
    {
        // Arrange — the next run adds to these counts; leaving the retried row counted would push the
        // totals above TotalRowCount
        var process = PartlyAppliedRun();

        // Act
        await Resume(process.Id, retryFailed: true);

        // Assert
        process.SucceededRowCount.Should().Be(1);
        process.FailedRowCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SkipsRowsThePurgeEmptiedAndReportsHowMany()
    {
        // Arrange — past its retention window, so there is nothing left to execute
        var process = PartlyAppliedRun();
        process.Rows.Single(r => r.ImportId == "r2").PurgePayload();

        // Act
        var result = await Resume(process.Id, retryFailed: true);

        // Assert — the rest of the run still goes ahead
        result.Value.SkippedRowCount.Should().Be(1);
        result.Value.QueuedRowCount.Should().Be(1);
        process.Rows.Single(r => r.ImportId == "r2").Status.Should().Be(ImportRowStatus.Failed);
    }

    [Fact]
    public async Task Handle_WhenEveryRemainingRowWasPurged_Fails()
    {
        // Arrange
        var process = QueueRun(1);
        var row = process.Rows.Single();
        row.MarkFailed("Rejected.", _now);
        row.PurgePayload();
        process.RecordProgress(succeeded: 0, failed: 1, _now);
        process.Complete(_now);

        // Act
        var result = await Resume(process.Id, retryFailed: true);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("retention window");
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenNothingIsLeftToApply_Fails()
    {
        // Arrange — every row already succeeded
        var process = QueueRun(1);
        process.Rows.Single().MarkSucceeded(Guid.CreateVersion7(), _now);
        process.RecordProgress(succeeded: 1, failed: 0, _now);
        process.Complete(_now);

        // Act
        var result = await Resume(process.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
    }

    [Fact]
    public async Task Handle_RefusesARunThatIsStillGoing()
    {
        // Arrange — requeuing a live run would have two workers applying the same rows
        var process = QueueRun(1);

        // Act
        var result = await Resume(process.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Processing");
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_RefusesACallerWithoutThePermissionTheImportDeclares()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var process = PartlyAppliedRun();

        // Act
        var result = await Resume(process.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelled);
    }
}
