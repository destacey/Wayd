using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class RecoverStalledImportsCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly Mock<IDispatcher> _dispatcher = new();

    public void Dispose() => _db.Dispose();

    private RecoverStalledImportsCommandHandler CreateHandler()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);

        return new RecoverStalledImportsCommandHandler(
            _db, clock.Object, _dispatcher.Object, NullLogger<RecoverStalledImportsCommandHandler>.Instance);
    }

    private ImportProcess QueuedAt(Instant submittedOn)
    {
        var rows = new[] { ImportProcessRow.Create("r1", 1, "{}") };
        var process = ImportProcess.Create("test-import", "user-1", null, rows, submittedOn);
        _db.AddImportProcess(process);
        return process;
    }

    private ImportProcess RunningSince(Instant lastProgressOn)
    {
        var process = QueuedAt(lastProgressOn);
        process.Start("trace-1", lastProgressOn);
        return process;
    }

    private Task<CSharpFunctionalExtensions.Result<StalledImportRecovery>> Recover() =>
        CreateHandler().Handle(new RecoverStalledImportsCommand(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_PublishesAgainForARunLeftQueuedPastTheGrace()
    {
        // Arrange — submitted half an hour ago and never claimed, so its message never arrived
        var process = QueuedAt(_now - Duration.FromMinutes(30));

        // Act
        var result = await Recover();

        // Assert — the run is left Queued, which is the only state a worker can claim it from
        result.Value.Republished.Should().Be(1);
        process.Status.Should().Be(ImportProcessStatus.Queued);
        _dispatcher.Verify(
            d => d.Publish(
                It.Is<RunImportProcessCommand>(c => c.ImportProcessId == process.Id),
                // The submitter, not this sweep: it runs as the system and must not claim authorship of
                // the records the reclaimed run goes on to create.
                process.SubmittedByUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LeavesARunQueuedOnlyMomentsAgoAlone()
    {
        // Arrange — a normal queue delay, not a lost message
        QueuedAt(_now - Duration.FromMinutes(2));

        // Act
        var result = await Recover();

        // Assert
        result.Value.Republished.Should().Be(0);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_FailsARunThatStoppedReportingProgress()
    {
        // Arrange — claimed two hours ago and silent since
        var process = RunningSince(_now - Duration.FromHours(2));

        // Act
        var result = await Recover();

        // Assert
        result.Value.Failed.Should().Be(1);
        process.Status.Should().Be(ImportProcessStatus.Failed);
        process.Error.Should().Contain("resumed");
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LeavesTheUnappliedRowsOfAStalledRunPending()
    {
        // Arrange — Resume is the recovery path, and it claims rows by status
        var process = RunningSince(_now - Duration.FromHours(2));

        // Act
        await Recover();

        // Assert
        process.Rows.Should().AllSatisfy(r => r.Status.Should().Be(ImportRowStatus.Pending));
    }

    [Fact]
    public async Task Handle_NeverRepublishesARunThatWasClaimed()
    {
        // Arrange — a stalled run is failed, not retried: a silent worker cannot be told from a dead one,
        // and reapplying rows the first is still working through would double them
        RunningSince(_now - Duration.FromHours(2));

        // Act
        await Recover();

        // Assert
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_LeavesARunThatIsStillReportingProgressAlone()
    {
        // Arrange — a long import that heartbeats at every chunk boundary
        var process = RunningSince(_now - Duration.FromHours(4));
        process.RecordProgress(succeeded: 0, failed: 0, _now - Duration.FromMinutes(1));

        // Act
        var result = await Recover();

        // Assert
        result.Value.Failed.Should().Be(0);
        process.Status.Should().Be(ImportProcessStatus.Processing);
    }

    [Fact]
    public async Task Handle_SweepsARunLeftMidCancellation()
    {
        // Arrange — the worker died between the cancellation request and acting on it
        var process = RunningSince(_now - Duration.FromHours(2));
        process.RequestCancellation(_now - Duration.FromHours(2));

        // Act
        var result = await Recover();

        // Assert
        result.Value.Failed.Should().Be(1);
        process.Status.Should().Be(ImportProcessStatus.Failed);
    }

    [Fact]
    public async Task Handle_LeavesAFinishedRunAlone()
    {
        // Arrange
        var process = RunningSince(_now - Duration.FromDays(3));
        process.Complete(_now - Duration.FromDays(3));

        // Act
        var result = await Recover();

        // Assert
        result.Value.Should().Be(new StalledImportRecovery(0, 0));
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
    }
}
