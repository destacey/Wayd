using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Planning.IntegrationTests.Infrastructure;

/// <summary>
/// Starts another request, on a second context, just before the first save of the context under test, and lets that
/// save go ahead once the other request is waiting on a lock — the request that arrives while the one under test holds
/// its lock, and so must act only after it commits.
/// </summary>
/// <remarks>
/// If the other request finishes without ever waiting, the save goes ahead anyway: the lock it should have met was not
/// held, and the test's assertions on what each request did catch that.
/// </remarks>
internal sealed class WaitingRequest<T>(SqlServerDbContextFixture fixture, Func<WaydDbContext, CancellationToken, Task<T>> request) : SaveChangesInterceptor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private Task<T>? _completion;

    /// <summary>
    /// The other request's result, once the context under test has saved; awaiting it waits for the request to finish.
    /// </summary>
    /// <exception cref="InvalidOperationException">The context under test never saved, so the request never started.</exception>
    public Task<T> Completion => _completion
        ?? throw new InvalidOperationException("The context under test never saved, so the waiting request never started.");

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_completion is null)
        {
            var sessionId = new TaskCompletionSource<short>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = Task.Run(async () =>
            {
                await using var context = fixture.CreateContext();
                // Opened here so the session id read is the one the request's transaction then runs on.
                await context.Database.OpenConnectionAsync(cancellationToken);
                sessionId.SetResult(await context.Database
                    .SqlQuery<short>($"SELECT CAST(@@SPID AS smallint) AS [Value]")
                    .SingleAsync(cancellationToken));
                return await request(context, cancellationToken);
            }, cancellationToken);

            await Task.WhenAny(sessionId.Task, _completion);
            if (sessionId.Task.IsCompletedSuccessfully)
                await UntilWaitingOnLockOrDone(sessionId.Task.Result, cancellationToken);
        }

        return result;
    }

    private async Task UntilWaitingOnLockOrDone(short sessionId, CancellationToken cancellationToken)
    {
        await using var observer = fixture.CreateContext();
        var waited = System.Diagnostics.Stopwatch.StartNew();

        while (!_completion!.IsCompleted)
        {
            var waiting = await observer.Database
                .SqlQuery<int>($@"
                    SELECT COUNT(*) AS [Value]
                    FROM sys.dm_exec_requests
                    WHERE [session_id] = {sessionId} AND [wait_type] LIKE 'LCK[_]%'")
                .SingleAsync(cancellationToken);
            if (waiting > 0)
                return;

            if (waited.Elapsed > Timeout)
                throw new TimeoutException("The other request neither finished nor waited on a lock.");

            await Task.Delay(25, cancellationToken);
        }
    }
}
