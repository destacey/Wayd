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
}