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

public sealed class RunImportProcessCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly TestImportDefinition _definition = new(new ImportPayloadSerializer());

    public void Dispose() => _db.Dispose();

    private RunImportProcessCommandHandler CreateHandler()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);

        return new RunImportProcessCommandHandler(
            _db,
            new ImportDefinitionRegistry([_definition]),
            clock.Object,
            NullLogger<RunImportProcessCommandHandler>.Instance);
    }

    /// <summary>Queues a run of <paramref name="rowCount"/> rows, failing the rows named in <paramref name="failing"/>.</summary>
    private ImportProcess QueueRun(int rowCount, params string[] failing) =>
        QueueRun(rowCount, failing, failingInLink: []);

    private ImportProcess QueueRun(int rowCount, string[] failing, string[] failingInLink)
    {
        var rows = Enumerable.Range(1, rowCount).Select(i =>
        {
            var importId = $"r{i}";
            var payload = _definition.SerializeRow(
                new TestImportRow($"Row {i}", failing.Contains(importId), failingInLink.Contains(importId)));
            return ImportProcessRow.Create(importId, i, payload);
        });

        var process = ImportProcess.Create("test-import", "user-1", null, rows, _now);
        _db.AddImportProcess(process);
        return process;
    }

    private Task<CSharpFunctionalExtensions.Result> Run(ImportProcess process) =>
        CreateHandler().Handle(new RunImportProcessCommand(process.Id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_RunsPassesInOrderAndChunksOnlyTheChunkableOne()
    {
        // Arrange — 5 rows, chunk size 2: Create runs three times, Link once over everything
        var process = QueueRun(5);

        // Act
        await Run(process);

        // Assert
        _definition.Calls.Select(c => c.Pass).Should().Equal("Create", "Create", "Create", "Link");
        _definition.Calls[0].ImportIds.Should().Equal("r1", "r2");
        _definition.Calls[2].ImportIds.Should().Equal("r5");
        _definition.Calls[2].IsFinalChunk.Should().BeTrue();
        _definition.Calls[3].ImportIds.Should().HaveCount(5);
    }

    [Fact]
    public async Task Handle_MarksRowsSucceededOnlyAfterTheFinalPass()
    {
        // Arrange — asserts during the last pass that nothing has been settled yet
        var process = QueueRun(2);
        var statusesDuringLink = Array.Empty<ImportRowStatus>();
        _definition.BeforePass = () => statusesDuringLink = [.. process.Rows.Select(r => r.Status)];

        // Act
        await Run(process);

        // Assert
        statusesDuringLink.Should().AllBeEquivalentTo(ImportRowStatus.Pending);
        process.Rows.Should().AllSatisfy(r => r.Status.Should().Be(ImportRowStatus.Succeeded));
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
        process.SucceededRowCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_IgnoresARedeliveryOfARunAlreadyClaimed()
    {
        // Arrange — the at-least-once case
        var process = QueueRun(2);
        process.Start("earlier-trace", _now);

        // Act
        var result = await Run(process);

        // Assert — nothing re-applied
        result.IsSuccess.Should().BeTrue();
        _definition.Calls.Should().BeEmpty();
        process.LastAttemptCorrelationId.Should().Be("earlier-trace");
    }

    [Fact]
    public async Task Handle_DropsARejectedRowFromTheLaterPasses()
    {
        // Arrange
        var process = QueueRun(3, failing: "r2");

        // Act
        await Run(process);

        // Assert — the Link pass never sees r2
        _definition.Calls.Last().Pass.Should().Be("Link");
        _definition.Calls.Last().ImportIds.Should().Equal("r1", "r3");

        process.Rows.Single(r => r.ImportId == "r2").Status.Should().Be(ImportRowStatus.Failed);
        process.Status.Should().Be(ImportProcessStatus.PartiallySucceeded);
        process.SucceededRowCount.Should().Be(2);
        process.FailedRowCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FailsTheRunWhenAPassCannotExecute()
    {
        // Arrange
        var process = QueueRun(2);
        _definition.PassFailure = "The lookup table is missing.";

        // Act
        var result = await Run(process);

        // Assert — distinct from rows failing: the run itself ends
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Failed);
        process.Error.Should().Contain("The lookup table is missing.");
    }

    [Fact]
    public async Task Handle_StopsAtTheNextChunkWhenCancellationIsRequested()
    {
        // Arrange — cancellation lands while the first chunk is running
        var process = QueueRun(6);
        _definition.BeforePass = () =>
        {
            process.RequestCancellation(_now);
            _definition.BeforePass = null;
        };

        // Act
        await Run(process);

        // Assert — the chunk in flight finished its pass; nothing further was started. Its rows are not
        // applied, since the Link pass never reached them, but they keep the progress a resume carries on from
        _definition.Calls.Should().ContainSingle();
        process.Status.Should().Be(ImportProcessStatus.Cancelled);
        process.Rows.Should().AllSatisfy(r => r.Status.Should().Be(ImportRowStatus.Cancelled));
        process.Rows.Where(r => r.ImportId is "r1" or "r2").Should().AllSatisfy(r => r.CompletedPassCount.Should().Be(1));
    }

    [Fact]
    public async Task Handle_KeepsTheWholeFileOutWhenAnAtomicImportRejectsARow()
    {
        // Arrange
        var process = QueueRun(4, failing: "r3");
        _definition.AtomicityOverride = ImportAtomicity.Atomic;

        // Act
        await Run(process);

        // Assert — nothing is left succeeded, and the reason says why
        process.Status.Should().Be(ImportProcessStatus.Failed);
        process.Rows.Should().NotContain(r => r.Status == ImportRowStatus.Succeeded);
        process.Rows.Should().Contain(r => r.Error!.Contains("all-or-nothing"));
    }

    [Fact]
    public async Task Handle_RecordsNothingForTheAcceptedRowsWhenAnAtomicImportRejectsOneInItsLastPass()
    {
        // Arrange — the rejection comes in the pass that would otherwise settle the accepted rows
        var process = QueueRun(4, failing: [], failingInLink: ["r3"]);
        _definition.AtomicityOverride = ImportAtomicity.Atomic;

        // Act
        await Run(process);

        // Assert — every row counted as failed, none as applied, and none advanced past the saved pass
        process.Status.Should().Be(ImportProcessStatus.Failed);
        process.Rows.Should().AllSatisfy(r => r.Status.Should().Be(ImportRowStatus.Failed));
        process.Rows.Single(r => r.ImportId == "r3").Error.Should().Contain("last pass");
        process.FailedRowCount.Should().Be(4);
        process.SucceededRowCount.Should().Be(0);
        process.Rows.Should().AllSatisfy(r => r.CompletedPassCount.Should().Be(1));
    }

    [Fact]
    public async Task Handle_FailsTheRunWhenItsDefinitionIsNoLongerRegistered()
    {
        // Arrange — a run persisted under a definition that has since been removed
        var rows = new[] { ImportProcessRow.Create("r1", 1, "{}") };
        var process = ImportProcess.Create("retired-import", "user-1", null, rows, _now);
        _db.AddImportProcess(process);

        // Act
        var result = await Run(process);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Failed);
        process.Error.Should().Contain("retired-import");
    }

    [Fact]
    public async Task Handle_FailsWhenTheRunDoesNotExist()
    {
        // Arrange & Act
        var result = await CreateHandler().Handle(
            new RunImportProcessCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("was not found");
    }

    [Fact]
    public async Task Handle_SavesEachChunkSoACrashCannotLeaveAppliedRowsPending()
    {
        // Arrange — 5 rows over three Create chunks, then Link
        var process = QueueRun(5);

        // Act
        await Run(process);

        // Assert — a save claiming the run, one per chunk, and one completing it
        _db.SaveChangesCallCount.Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task Handle_ReleasesARunThatThrewBeforeSavingAnythingAndRethrows()
    {
        // Arrange — the first chunk throws before its save, as a database blip would
        var process = QueueRun(4);
        _definition.BeforePass = () => throw new InvalidOperationException("Transient failure.");

        // Act
        var act = () => Run(process);

        // Assert — rethrown so the failure policy retries, and claimable again when it does
        await act.Should().ThrowAsync<InvalidOperationException>();
        process.Status.Should().Be(ImportProcessStatus.Queued);
        process.AttemptCount.Should().Be(1);
        process.Rows.Should().AllSatisfy(r => r.Status.Should().Be(ImportRowStatus.Pending));
    }

    [Fact]
    public async Task Handle_CompletesARunOnTheDeliveryAfterItWasReleased()
    {
        // Arrange — one failed attempt, then the retry
        var process = QueueRun(2);
        _definition.BeforePass = () =>
        {
            _definition.BeforePass = null;
            throw new InvalidOperationException("Transient failure.");
        };
        await FluentActions.Awaiting(() => Run(process)).Should().ThrowAsync<InvalidOperationException>();

        // Act
        var result = await Run(process);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
        process.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_EndsTheRunOnItsFinalAttemptInsteadOfReleasingIt()
    {
        // Arrange — a failure that never clears
        var process = QueueRun(2);
        _definition.BeforePass = () => throw new InvalidOperationException("Permanent failure.");

        for (var attempt = 1; attempt < ImportProcess.MaxAttempts; attempt++)
            await FluentActions.Awaiting(() => Run(process)).Should().ThrowAsync<InvalidOperationException>();

        // Act
        var result = await Run(process);

        // Assert — settled here, not handed back to a retry that would find it claimed
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Failed);
        process.AttemptCount.Should().Be(ImportProcess.MaxAttempts);
        process.Error.Should().Contain($"{ImportProcess.MaxAttempts} attempts");
    }

    [Fact]
    public async Task Handle_RetriesARunThatThrewAfterSavingAChunkWithoutReapplyingIt()
    {
        // Arrange — chunk one (r1, r2) saves its Create pass; chunk two throws
        var process = QueueRun(4);
        var calls = 0;
        _definition.BeforePass = () =>
        {
            if (++calls == 2)
                throw new InvalidOperationException("Failure after a save.");
        };
        await FluentActions.Awaiting(() => Run(process)).Should().ThrowAsync<InvalidOperationException>();
        process.Status.Should().Be(ImportProcessStatus.Queued);
        _definition.Calls.Clear();
        _definition.BeforePass = null;

        // Act — the failure policy's next delivery
        await Run(process);

        // Assert — Create runs only for the rows it had not reached; Link for all of them
        _definition.Calls.Select(c => (c.Pass, string.Join(",", c.ImportIds)))
            .Should().Equal(("Create", "r3,r4"), ("Link", "r1,r2,r3,r4"));
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
        process.SucceededRowCount.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ResumesAFailedPassFromEachRowsProgress()
    {
        // Arrange — the second Create chunk cannot run, after the first has saved
        var process = QueueRun(4);
        var calls = 0;
        _definition.BeforePass = () =>
        {
            if (++calls == 2)
                _definition.PassFailure = "The lookup table is missing.";
        };
        await Run(process);
        process.Status.Should().Be(ImportProcessStatus.Failed);

        process.Requeue(_now);
        _definition.Calls.Clear();
        _definition.BeforePass = null;
        _definition.PassFailure = null;

        // Act — a person resumes it once the cause is fixed
        await Run(process);

        // Assert — r1 and r2 are not created a second time
        _definition.Calls.Select(c => (c.Pass, string.Join(",", c.ImportIds)))
            .Should().Equal(("Create", "r3,r4"), ("Link", "r1,r2,r3,r4"));
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
    }

    [Fact]
    public async Task Handle_CancelsARunThatThrewWhileAStopWasRequested()
    {
        // Arrange — the stop arrives, then the attempt fails
        var process = QueueRun(2);
        _definition.BeforePass = () =>
        {
            process.RequestCancellation(_now);
            throw new InvalidOperationException("Failure while stopping.");
        };

        // Act
        var result = await Run(process);

        // Assert — the person asked for it to stop, so it is not retried
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelled);
    }
}
