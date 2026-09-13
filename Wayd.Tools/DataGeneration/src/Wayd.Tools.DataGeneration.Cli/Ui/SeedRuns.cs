namespace Wayd.Tools.DataGeneration.Cli.Ui;

/// <summary>Where a seed started from the page has got to.</summary>
public enum SeedRunState
{
    Running,
    Succeeded,
    Failed,
    Canceled,
}

/// <summary>
/// One seed started from the page: its log so far, and how it ended.
/// </summary>
/// <remarks>
/// The log is kept rather than only streamed, so a page that reconnects — or connects after the first lines
/// were written — reads the whole run from the start.
/// </remarks>
public sealed class SeedRun
{
    private readonly Lock _lock = new();
    private readonly List<string> _lines = [];

    internal SeedRun(CancellationToken stopping)
    {
        Cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
    }

    public Guid Id { get; } = Guid.NewGuid();

    internal CancellationTokenSource Cancellation { get; }

    public SeedRunState State { get; private set; } = SeedRunState.Running;

    public string? Error { get; private set; }

    public void Log(string line)
    {
        lock (_lock)
            _lines.Add(line);
    }

    /// <summary>The lines written since <paramref name="from"/>, and the state they were read under.</summary>
    /// <remarks>
    /// Read together under the lock. A run's last lines are written before it finishes, so a caller that sees
    /// a finished state here has also been handed everything the run will ever write.
    /// </remarks>
    public (IReadOnlyList<string> Lines, SeedRunState State, string? Error) Read(int from)
    {
        lock (_lock)
            return ([.. _lines.Skip(from)], State, Error);
    }

    internal void Finish(SeedRunState state, string? error = null)
    {
        lock (_lock)
        {
            State = state;
            Error = error;
        }
    }

    /// <summary>Asks the run to stop. False when it has already ended.</summary>
    /// <remarks>
    /// The state is checked under the lock <see cref="Finish"/> takes, so an ended run never touches its
    /// cancellation source — which is what lets <see cref="SeedRuns"/> dispose it once the run is replaced.
    /// Cancelling runs the token's callbacks on this thread, and the seed's own continuation can run inline
    /// and finish the run; that re-enters the lock on the same thread, which <see cref="Lock"/> allows.
    /// </remarks>
    internal bool Cancel()
    {
        lock (_lock)
        {
            if (State != SeedRunState.Running)
                return false;

            Cancellation.Cancel();
            return true;
        }
    }
}

/// <summary>
/// The page's seeds, one at a time.
/// </summary>
/// <remarks>
/// One at a time because two seeds into the same environment would race each other's imports, and a seed
/// is long enough that a second click while the first is running is far more likely to be a mistake than a
/// plan.
/// <para>
/// Only the latest run is kept. The page follows the one it started, and holding every past run's log would
/// grow for as long as the tool is left open.
/// </para>
/// </remarks>
public sealed class SeedRuns(CancellationToken stopping)
{
    private readonly Lock _lock = new();
    private readonly CancellationToken _stopping = stopping;
    private SeedRun? _current;

    /// <summary>Starts <paramref name="work"/> as a run, unless one is still going.</summary>
    public SeedRun? TryStart(Func<SeedRun, CancellationToken, Task> work)
    {
        SeedRun run;
        lock (_lock)
        {
            if (_current is { State: SeedRunState.Running })
                return null;

            // The run being replaced has ended, so nothing will cancel through its source again.
            _current?.Cancellation.Dispose();

            run = new SeedRun(_stopping);
            _current = run;
        }

        var cancellation = run.Cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await work(run, cancellation);
                run.Finish(SeedRunState.Succeeded);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                run.Log("Canceled. Imports the API had already accepted still finish there.");
                run.Finish(SeedRunState.Canceled);
            }
            catch (Exception ex)
            {
                run.Finish(SeedRunState.Failed, ex.Message);
            }
        }, CancellationToken.None);

        return run;
    }

    public SeedRun? Find(Guid id)
    {
        lock (_lock)
            return _current?.Id == id ? _current : null;
    }

    /// <summary>Asks a run to stop. False when there is no such run, or it has already ended.</summary>
    public bool Cancel(Guid id) => Find(id)?.Cancel() ?? false;
}
