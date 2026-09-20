namespace Wayd.Infrastructure.Persistence;

public class DatabaseSettings
{
    public string? DBProvider { get; set; }
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Includes parameter values in EF Core's command logging. Invaluable when debugging a failing query
    /// locally and unacceptable anywhere else: a failed bulk insert logs every parameter of every row, so a
    /// real sync emits employee names and email addresses by the thousand — a privacy problem in its own
    /// right, and enough volume to get the entry truncated by the log sink that would have carried the
    /// actual error.
    /// <para>
    /// Defaults to <c>false</c>. <c>appsettings.Development.json</c> turns it on.
    /// </para>
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// How long any one command may run, in seconds. Defaults to 30, which is also the provider's own
    /// default.
    /// </summary>
    /// <remarks>
    /// The ceiling belongs here rather than at each call site: a request-path write that takes half a minute
    /// is a fault worth surfacing, and raising the limit everywhere to accommodate the few operations that
    /// legitimately run long would hide it. Those operations ask for longer explicitly and for their own
    /// scope — see <c>BaseDbContext.WithCommandTimeout</c>, used by the migration run and by the import
    /// runner, whose files are bounded only by their row cap.
    /// </remarks>
    public int CommandTimeoutSeconds { get; set; } = 30;
}