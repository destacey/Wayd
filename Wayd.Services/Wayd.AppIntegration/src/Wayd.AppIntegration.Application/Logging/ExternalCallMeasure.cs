using System.Diagnostics;
using Wayd.Common.Application.Logging;

namespace Wayd.AppIntegration.Application.Logging;

public static class ExternalCallMeasure
{
    private static readonly Action<ILogger, string, long, Exception?> _externalCallElapsed =
    LoggerMessage.Define<string, long>(LogLevel.Information, AppEventId.AppIntegration_ExternalCallElapsed.ToEventId(), "External call {CallName} completed in {ElapsedMs}ms");

    private static readonly Action<ILogger, string, Guid, long, Exception?> _externalCallElapsedWithSyncId =
    LoggerMessage.Define<string, Guid, long>(LogLevel.Information, AppEventId.AppIntegration_ExternalCallElapsed.ToEventId(), "External call {CallName}; SyncId={SyncId}; ElapsedMs={ElapsedMs}ms");

    public static async Task<T> MeasureAsync<T>(ILogger logger, string callName, Func<Task<T>> func, Guid? syncId = null)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return await func();
        }
        finally
        {
            var elapsedMs = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            try
            {
                if (syncId.HasValue)
                    _externalCallElapsedWithSyncId(logger, callName, syncId.Value, elapsedMs, null);
                else
                    _externalCallElapsed(logger, callName, elapsedMs, null);
            }
            catch
            {
                // swallow logging errors
            }
        }
    }

    public static async Task MeasureAsync(ILogger logger, string callName, Func<Task> func, Guid? syncId = null)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await func();
        }
        finally
        {
            var elapsedMs = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            try
            {
                if (syncId.HasValue)
                    _externalCallElapsedWithSyncId(logger, callName, syncId.Value, elapsedMs, null);
                else
                    _externalCallElapsed(logger, callName, elapsedMs, null);
            }
            catch
            {
                // swallow logging errors
            }
        }
    }
}
