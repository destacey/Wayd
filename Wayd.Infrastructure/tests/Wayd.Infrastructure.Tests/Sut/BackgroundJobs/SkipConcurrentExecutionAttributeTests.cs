using System.Diagnostics;
using FluentAssertions;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.States;
using Wayd.Infrastructure.BackgroundJobs;

namespace Wayd.Infrastructure.Tests.Sut.BackgroundJobs;

/// <summary>
/// Against a real Hangfire server: the skip depends on the worker turning a canceled run into Deleted and on the
/// election that follows, which only a running server performs.
/// </summary>
public sealed class SkipConcurrentExecutionAttributeTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    // Hangfire only runs public methods, and a public non-test method on the test class trips xUnit1013.
    public static class Jobs
    {
        internal static SemaphoreSlim Started = new(0);
        internal static SemaphoreSlim Finish = new(0);

        [SkipConcurrentExecution]
        public static async Task Held()
        {
            Started.Release();
            await Finish.WaitAsync();
        }

        [SkipConcurrentExecution]
        public static Task Other() => Task.CompletedTask;

        [SkipConcurrentExecution]
        [AutomaticRetry(Attempts = 0)]
        public static async Task HeldThenFails()
        {
            Started.Release();
            await Finish.WaitAsync();
            throw new InvalidOperationException("The run failed.");
        }

        /// <summary>A run for one scope, covered only by a run for the same scope; an uncovered run waits up to ten seconds.</summary>
        [SkipConcurrentExecution(typeof(SameScope), 10)]
        [AutomaticRetry(Attempts = 0)]
        public static async Task HeldFor(string scope)
        {
            Started.Release();
            await Finish.WaitAsync();
        }

        /// <summary>As <see cref="HeldFor"/>, but an uncovered run waits only one second.</summary>
        [SkipConcurrentExecution(typeof(SameScope), 1)]
        [AutomaticRetry(Attempts = 0)]
        public static async Task HeldBriefly(string scope)
        {
            Started.Release();
            await Finish.WaitAsync();
        }
    }

    /// <summary>A run covers another of the same method when both are for the same scope.</summary>
    public sealed class SameScope : IRunCoverage
    {
        public bool Covers(Hangfire.Common.Job running, Hangfire.Common.Job started) =>
            Equals(running.Args[0], started.Args[0]);
    }

    private readonly InMemoryStorage _storage = new();
    private readonly BackgroundJobClient _client;
    private readonly BackgroundJobServer _server;

    public SkipConcurrentExecutionAttributeTests()
    {
        Jobs.Started = new SemaphoreSlim(0);
        Jobs.Finish = new SemaphoreSlim(0);
        _client = new BackgroundJobClient(_storage);
        _server = new BackgroundJobServer(new BackgroundJobServerOptions { WorkerCount = 3 }, _storage);
    }

    public void Dispose()
    {
        // Let any held run finish so the server's shutdown does not wait on it.
        Jobs.Finish.Release(10);
        _server.Dispose();
        _storage.Dispose();
    }

    [Fact]
    public async Task Run_IsSkippedWithTheReason_WhenAnotherRunOfTheSameJobIsInProgress()
    {
        // Arrange
        var running = await StartHeldRun();

        // Act
        var overlapping = _client.Enqueue(() => Jobs.Held());

        // Assert — at once, not after waiting for the lock
        var skipped = await WaitForState(overlapping, DeletedState.StateName, TimeSpan.FromSeconds(5));
        skipped.Reason.Should().Be("Skipped: another run of Held was already in progress.");
        CurrentState(running).Should().Be(ProcessingState.StateName);
    }

    [Fact]
    public async Task Run_TakesTheLock_OnceThePreviousRunHasFinished()
    {
        // Arrange
        var first = await StartHeldRun();
        Jobs.Finish.Release();
        await WaitForState(first, SucceededState.StateName);

        // Act
        var second = _client.Enqueue(() => Jobs.Held());
        (await Jobs.Started.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Should().BeTrue();
        Jobs.Finish.Release();

        // Assert
        await WaitForState(second, SucceededState.StateName);
    }

    [Fact]
    public async Task Run_TakesTheLock_OnceThePreviousRunHasFailed()
    {
        // Arrange
        var failing = _client.Enqueue(() => Jobs.HeldThenFails());
        (await Jobs.Started.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Should().BeTrue();
        Jobs.Finish.Release();
        await WaitForState(failing, FailedState.StateName);

        // Act
        var next = _client.Enqueue(() => Jobs.HeldThenFails());

        // Assert
        (await Jobs.Started.WaitAsync(Timeout, TestContext.Current.CancellationToken))
            .Should().BeTrue("the failed run should have released the lock");
        Jobs.Finish.Release();
        await WaitForState(next, FailedState.StateName);
    }

    [Fact]
    public async Task Run_IsSkipped_WhenTheRunInProgressCoversIt()
    {
        // Arrange
        await StartRun(() => Jobs.HeldFor("a"));

        // Act
        var covered = _client.Enqueue(() => Jobs.HeldFor("a"));

        // Assert
        var skipped = await WaitForState(covered, DeletedState.StateName, TimeSpan.FromSeconds(5));
        skipped.Reason.Should().Be("Skipped: another run of HeldFor was already in progress.");
    }

    [Fact]
    public async Task Run_WaitsForTheLockAndRuns_WhenTheRunInProgressDoesNotCoverIt()
    {
        // Arrange
        var running = await StartRun(() => Jobs.HeldFor("a"));

        // Act — still waiting well after it found the lock held, then released
        var uncovered = _client.Enqueue(() => Jobs.HeldFor("b"));
        await WaitForState(uncovered, ProcessingState.StateName);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        CurrentState(uncovered).Should().Be(ProcessingState.StateName, "an uncovered run waits for the lock rather than failing");
        Jobs.Finish.Release();

        // Assert
        await WaitForState(running, SucceededState.StateName);
        (await Jobs.Started.WaitAsync(Timeout, TestContext.Current.CancellationToken))
            .Should().BeTrue("the uncovered run should have run once the lock was free");
        Jobs.Finish.Release();
        await WaitForState(uncovered, SucceededState.StateName);
    }

    [Fact]
    public async Task Run_Waits_WhenOnlyARunStillWaitingForTheLockCoversIt()
    {
        // Arrange — "b" waits behind the holder "a", which covers neither
        await StartRun(() => Jobs.HeldFor("a"));
        var waiting = _client.Enqueue(() => Jobs.HeldFor("b"));
        await WaitForState(waiting, ProcessingState.StateName);

        // Act — a second "b" is covered by the waiting one, which may yet time out or be overtaken
        var alsoWaiting = _client.Enqueue(() => Jobs.HeldFor("b"));
        await WaitForState(alsoWaiting, ProcessingState.StateName);
        Jobs.Finish.Release();
        for (var run = 0; run < 2; run++)
        {
            (await Jobs.Started.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Should().BeTrue();
            Jobs.Finish.Release();
        }

        // Assert
        await WaitForState(waiting, SucceededState.StateName);
        await WaitForState(alsoWaiting, SucceededState.StateName);
    }

    [Fact]
    public async Task Run_Fails_WhenTheRunInProgressDoesNotCoverItAndOutlastsTheWait()
    {
        // Arrange
        await StartRun(() => Jobs.HeldBriefly("a"));

        // Act
        var uncovered = _client.Enqueue(() => Jobs.HeldBriefly("b"));

        // Assert
        var failed = await WaitForState(uncovered, FailedState.StateName);
        failed.Data["ExceptionType"].Should().Be(typeof(Hangfire.Storage.DistributedLockTimeoutException).FullName);
    }

    [Fact]
    public async Task Run_IsNotSkipped_WhenADifferentJobIsInProgress()
    {
        // Arrange
        await StartHeldRun();

        // Act
        var other = _client.Enqueue(() => Jobs.Other());

        // Assert
        await WaitForState(other, SucceededState.StateName);
    }

    private Task<string> StartHeldRun() => StartRun(() => Jobs.Held());

    private async Task<string> StartRun(System.Linq.Expressions.Expression<Func<Task>> run)
    {
        var jobId = _client.Enqueue(run);
        (await Jobs.Started.WaitAsync(Timeout, TestContext.Current.CancellationToken))
            .Should().BeTrue("the first run should have started");
        return jobId;
    }

    private string? CurrentState(string jobId)
    {
        using var connection = _storage.GetConnection();
        return connection.GetStateData(jobId)?.Name;
    }

    /// <summary>Waits for the job to reach a state and returns that state, failing when it does not in time.</summary>
    private async Task<Hangfire.Storage.StateData> WaitForState(string jobId, string state, TimeSpan? within = null)
    {
        var limit = within ?? Timeout;
        var waited = Stopwatch.StartNew();
        while (true)
        {
            using (var connection = _storage.GetConnection())
            {
                var current = connection.GetStateData(jobId);
                if (current?.Name == state)
                    return current;

                if (waited.Elapsed > limit)
                    throw new TimeoutException($"Job {jobId} is {current?.Name ?? "missing"}, not {state}.");
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }
}
