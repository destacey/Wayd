using Hangfire.Common;
using Wayd.Common.Application.Enums;
using Wayd.Infrastructure.BackgroundJobs;

namespace Wayd.Web.Api.Services;

/// <summary>
/// When a Work or People Sync in progress does all the work of a newly started one: it syncs every connection or the
/// same one, and it is a full sync or of the same type. A differential does not cover a full sync, and one
/// connection's sync does not cover another's.
/// </summary>
/// <remarks>Reads the job's <c>syncType</c> and <c>connectionId</c> arguments by parameter name.</remarks>
public sealed class SyncRunCoverage : IRunCoverage
{
    private const string SyncTypeParameter = "syncType";
    private const string ConnectionIdParameter = "connectionId";

    /// <inheritdoc />
    public bool Covers(Job running, Job started)
    {
        var (runningType, runningConnection) = Read(running);
        var (startedType, startedConnection) = Read(started);

        var coversConnections = runningConnection is null || runningConnection == startedConnection;
        var coversSyncType = runningType == SyncType.Full || runningType == startedType;

        return coversConnections && coversSyncType;
    }

    private static (SyncType SyncType, Guid? ConnectionId) Read(Job job)
    {
        var parameters = job.Method.GetParameters();

        object? Argument(string name)
        {
            var index = Array.FindIndex(parameters, p => p.Name == name);
            if (index < 0)
                throw new InvalidOperationException($"{job.Method.Name} has no {name} parameter for {nameof(SyncRunCoverage)}.");
            return job.Args[index];
        }

        return ((SyncType)Argument(SyncTypeParameter)!, (Guid?)Argument(ConnectionIdParameter));
    }
}
