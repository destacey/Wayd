using NodaTime;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Domain.Tests.Sut.Imports;

public sealed class ImportProcessTests
{
    private static readonly Instant _submitted = Instant.FromUtc(2026, 9, 5, 9, 0, 0);
    private static readonly Instant _started = Instant.FromUtc(2026, 9, 5, 9, 1, 0);
    private static readonly Instant _later = Instant.FromUtc(2026, 9, 5, 9, 5, 0);

    [Fact]
    public void Create_StartsQueuedWithTheRowsCounted()
    {
        // Arrange & Act
        var process = new ImportProcessFaker().AsQueuedWith(rowCount: 3);

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Queued);
        process.TotalRowCount.Should().Be(3);
        process.Rows.Should().HaveCount(3);
        process.StartedOn.Should().BeNull();
        process.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void Start_ClaimsTheRunAndSetsTheHeartbeat()
    {
        // Arrange
        var process = new ImportProcessFaker().AsQueuedWith(rowCount: 2);

        // Act
        var result = process.Start(ImportProcessFakerExtensions.AttemptCorrelationId, _started);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Processing);
        process.StartedOn.Should().Be(_started);
        process.LastProgressOn.Should().Be(_started);
        process.LastAttemptCorrelationId.Should().Be(ImportProcessFakerExtensions.AttemptCorrelationId);
    }

    [Fact]
    public void Start_RecordsTheTraceOfTheAttemptThatIsRunningNow()
    {
        // Arrange — a run reclaimed after its worker died, then resumed
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);
        process.Fail("Worker died.", _later);
        process.Requeue(_later);

        // Act
        process.Start("trace-0002", _later);

        // Assert — the second attempt's audit trail is reachable, the first attempt's id is not retained
        process.LastAttemptCorrelationId.Should().Be("trace-0002");
    }

    [Fact]
    public void Start_RefusesARunAlreadyClaimed()
    {
        // Arrange — the redelivery case: a durable message arriving a second time
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);

        // Act
        var result = process.Start(ImportProcessFakerExtensions.AttemptCorrelationId, _later);

        // Assert — refused, and the original claim is untouched
        result.IsFailure.Should().BeTrue();
        process.StartedOn.Should().Be(_started);
    }

    [Fact]
    public void Start_RefusesAFinishedRun()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 1, _started);
        process.Complete(_later);

        // Act
        var result = process.Start(ImportProcessFakerExtensions.AttemptCorrelationId, _later);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
    }

    [Fact]
    public void RecordProgress_AdvancesCountsAndHeartbeatTogether()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 5, _started);

        // Act
        process.RecordProgress(succeeded: 3, failed: 1, _later);

        // Assert
        process.SucceededRowCount.Should().Be(3);
        process.FailedRowCount.Should().Be(1);
        process.LastProgressOn.Should().Be(_later);
    }

    [Theory]
    [InlineData(5, 0, ImportProcessStatus.Succeeded)]
    [InlineData(3, 2, ImportProcessStatus.PartiallySucceeded)]
    [InlineData(0, 5, ImportProcessStatus.Failed)]
    public void Complete_DerivesTheTerminalStatusFromTheCounts(int succeeded, int failed, ImportProcessStatus expected)
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 5, _started);
        process.RecordProgress(succeeded, failed, _later);

        // Act
        var result = process.Complete(_later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(expected);
        process.CompletedOn.Should().Be(_later);
    }

    [Fact]
    public void Complete_IsAllowedWhileCancelling()
    {
        // Arrange — the run finished naturally in the same moment a cancellation arrived
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);
        process.RecordProgress(succeeded: 2, failed: 0, _later);
        process.RequestCancellation(_later);

        // Act
        var result = process.Complete(_later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
    }

    [Fact]
    public void Complete_RefusesARunThatNeverStarted()
    {
        // Arrange
        var process = new ImportProcessFaker().AsQueuedWith(rowCount: 2);

        // Act
        var result = process.Complete(_later);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Queued);
    }

    [Fact]
    public void RequestCancellation_MovesToCancellingWithoutEndingTheRun()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 4, _started);

        // Act
        var result = process.RequestCancellation(_later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelling);
        process.CompletedOn.Should().BeNull();
        process.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void RequestCancellation_IsIdempotent()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 4, _started);
        process.RequestCancellation(_later);

        // Act
        var result = process.RequestCancellation(_later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelling);
    }

    [Fact]
    public void RequestCancellation_RefusesAFinishedRun()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 1, _started);
        process.Complete(_later);

        // Act
        var result = process.RequestCancellation(_later);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cancel_KeepsTheCountsOfWhatWasAlreadyApplied()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 10, _started);
        process.RecordProgress(succeeded: 4, failed: 0, _later);
        process.RequestCancellation(_later);

        // Act
        var result = process.Cancel(_later);

        // Assert — stopping does not undo the four rows that landed
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelled);
        process.SucceededRowCount.Should().Be(4);
        process.CompletedOn.Should().Be(_later);
    }

    [Fact]
    public void Fail_RecordsTheReasonAgainstTheRun()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 3, _started);

        // Act — how the stall sweep reclaims a run whose worker died
        var result = process.Fail("No progress for 30 minutes.", _later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Failed);
        process.Error.Should().Be("No progress for 30 minutes.");
        process.CompletedOn.Should().Be(_later);
    }

    [Fact]
    public void Fail_RefusesAFinishedRun()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 1, _started);
        process.Complete(_later);

        // Act
        var result = process.Fail("Too late.", _later);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
        process.Error.Should().BeNull();
    }

    [Fact]
    public void Requeue_ReturnsAStoppedRunToTheQueueAndClearsTheFailure()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 6, _started);
        Apply(process, succeeded: 2);
        process.Fail("Worker died.", _later);

        // Act
        var result = process.Requeue(_later);

        // Assert — resume keeps what landed and drops the terminal bookkeeping
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Queued);
        process.CompletedOn.Should().BeNull();
        process.Error.Should().BeNull();
        process.SucceededRowCount.Should().Be(2);
    }

    [Fact]
    public void Requeue_StopsCountingARowThatWasReturnedToThePending()
    {
        // Arrange — a retry resets the rejected row before requeuing, and the next run adds to these
        // counts: still counting it as failed would push the totals past TotalRowCount
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 3, _started);
        Apply(process, succeeded: 1, failed: 2);
        process.Complete(_later);

        foreach (var row in process.Rows.Where(r => r.Status == ImportRowStatus.Failed))
        {
            row.Reset();
        }

        // Act
        process.Requeue(_later);

        // Assert
        process.SucceededRowCount.Should().Be(1);
        process.FailedRowCount.Should().Be(0);
    }

    /// <summary>Settles rows and records the progress together, the way the runner does.</summary>
    private static void Apply(ImportProcess process, int succeeded = 0, int failed = 0)
    {
        var rows = process.Rows.ToList();

        for (var i = 0; i < succeeded; i++)
        {
            rows[i].MarkSucceeded(Guid.CreateVersion7(), _later);
        }

        for (var i = succeeded; i < succeeded + failed; i++)
        {
            rows[i].MarkFailed("Rejected.", _later);
        }

        process.RecordProgress(succeeded, failed, _later);
    }

    [Fact]
    public void Requeue_RefusesARunStillInFlight()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);

        // Act
        var result = process.Requeue(_later);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Processing);
    }

    [Fact]
    public void Start_CountsEachClaim()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);
        process.Release(_later);

        // Act
        process.Start("trace-0002", _later);

        // Assert
        process.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void Release_ReturnsAClaimedRunToTheQueue()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);

        // Act
        var result = process.Release(_later);

        // Assert — claimable by the next delivery, which Start alone would refuse
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Queued);
        process.LastProgressOn.Should().Be(_later);
        process.Start("trace-0002", _later).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Release_RefusesARunThatHasUsedItsAttempts()
    {
        // Arrange
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);
        for (var attempt = 1; attempt < ImportProcess.MaxAttempts; attempt++)
        {
            process.Release(_later);
            process.Start($"trace-{attempt}", _later);
        }

        // Act
        var result = process.Release(_later);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.CanReleaseForRetry.Should().BeFalse();
        process.Status.Should().Be(ImportProcessStatus.Processing);
    }

    [Fact]
    public void Release_RefusesARunThatIsNotRunning()
    {
        // Arrange — a stop was requested; releasing would lose it
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);
        process.RequestCancellation(_later);

        // Act
        var result = process.Release(_later);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelling);
    }

    [Fact]
    public void Requeue_GivesTheRunAFreshSetOfAttempts()
    {
        // Arrange — a person resuming a run the runner gave up on
        var process = new ImportProcessFaker().AsProcessingWith(rowCount: 2, _started);
        process.Fail("Gave up.", _later);

        // Act
        process.Requeue(_later);

        // Assert
        process.AttemptCount.Should().Be(0);
        process.CanReleaseForRetry.Should().BeTrue();
    }
}
