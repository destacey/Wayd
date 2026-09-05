using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class PurgeExpiredImportPayloadsCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();

    public void Dispose() => _db.Dispose();

    private PurgeExpiredImportPayloadsCommandHandler CreateHandler()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);

        return new PurgeExpiredImportPayloadsCommandHandler(
            _db, clock.Object, NullLogger<PurgeExpiredImportPayloadsCommandHandler>.Instance);
    }

    private ImportProcess AddRun(int rowCount = 1)
    {
        var rows = Enumerable.Range(1, rowCount)
            .Select(i => ImportProcessRow.Create($"r{i}", i, $$"""{"name":"Row {{i}}"}"""));

        var process = ImportProcess.Create("test-import", "user-1", null, rows, _now - Duration.FromDays(120));
        _db.AddImportProcess(process);
        return process;
    }

    /// <summary>A run that failed <paramref name="daysAgo"/>, so its rows still hold their payloads.</summary>
    private ImportProcess FailedRun(int daysAgo, int rowCount = 1)
    {
        var finishedOn = _now - Duration.FromDays(daysAgo);
        var process = AddRun(rowCount);

        process.Start("trace-1", finishedOn);
        foreach (var row in process.Rows)
        {
            row.MarkFailed("Rejected.", finishedOn);
        }

        process.Fail("Every row was rejected.", finishedOn);
        return process;
    }

    private Task<CSharpFunctionalExtensions.Result<int>> Purge() =>
        CreateHandler().Handle(new PurgeExpiredImportPayloadsCommand(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ClearsThePayloadsOfARunFinishedBeyondRetention()
    {
        // Arrange
        var process = FailedRun(daysAgo: 45, rowCount: 3);

        // Act
        var result = await Purge();

        // Assert
        result.Value.Should().Be(3);
        process.Rows.Should().AllSatisfy(r => r.Payload.Should().BeNull());
    }

    [Fact]
    public async Task Handle_KeepsTheRunAndItsPerRowOutcomes()
    {
        // Arrange — the history is what the sweep is preserving; only the uploaded copy goes
        var process = FailedRun(daysAgo: 45);

        // Act
        await Purge();

        // Assert
        process.Rows.Single().Error.Should().Be("Rejected.");
        process.Error.Should().Be("Every row was rejected.");
    }

    [Fact]
    public async Task Handle_KeepsThePayloadsOfARecentlyFinishedRun()
    {
        // Arrange — still inside the window, so its failures can be retried
        var process = FailedRun(daysAgo: 29);

        // Act
        var result = await Purge();

        // Assert
        result.Value.Should().Be(0);
        process.Rows.Single().Payload.Should().NotBeNull();
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_KeepsThePayloadsOfARunThatHasNotFinished()
    {
        // Arrange — submitted long ago and still working, which a date filter alone would sweep
        var process = AddRun();
        process.Start("trace-1", _now - Duration.FromDays(120));

        // Act
        var result = await Purge();

        // Assert
        result.Value.Should().Be(0);
        process.Rows.Single().Payload.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ClearsEveryExpiredRowAcrossMoreThanOneBatch()
    {
        // Arrange — 501 rows against a batch of 500, so a single-pass sweep would leave one behind
        var process = FailedRun(daysAgo: 45, rowCount: 501);

        // Act
        var result = await Purge();

        // Assert
        result.Value.Should().Be(501);
        process.Rows.Should().AllSatisfy(r => r.Payload.Should().BeNull());
        _db.SaveChangesCallCount.Should().Be(2);
    }
}
