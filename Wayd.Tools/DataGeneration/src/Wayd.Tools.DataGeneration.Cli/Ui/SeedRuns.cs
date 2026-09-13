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

        Cancellation.Dispose();
    }
}

/// <summary>
/// The page's seeds, one at a time.
/// </summary>
/// <remarks>
/// One at a time because two seeds into the same environment would race each other's imports, and a seed
/// is long enough that a second click while the first is running is far more likely to be a mistake than a
/// plan.
/// </remarks>
public sealed class SeedRuns(CancellationToken stopping)
{
    private readonly Lock _lock = new();
    private readonly CancellationToken _stopping = stopping;
    private readonly Dictionary<Guid, SeedRun> _runs = [];
    private SeedRun? _current;

    /// <summary>Starts <paramref name="work"/> as a run, unless one is still going.</summary>
    public SeedRun? TryStart(Func<SeedRun, CancellationToken, Task> work)
    {
        SeedRun run;
        lock (_lock)
        {
            if (_current is { State: SeedRunState.Running })
                return null;

            run = new SeedRun(_stopping);
            _runs[run.Id] = run;
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
            return _runs.GetValueOrDefault(id);
    }

    /// <summary>Asks a run to stop. False when there is no such run, or it has already ended.</summary>
    public bool Cancel(Guid id)
    {
        var run = Find(id);
        if (run is null || run.State != SeedRunState.Running)
            return false;

        try
        {
            run.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // It finished between the check and the cancel.
            return false;
        }

        return true;
    }
}
