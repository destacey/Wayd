namespace Wayd.Common.Domain;

/// <summary>
/// Fixed messages for failures whose cause belongs in the log rather than in a response.
/// </summary>
public static class ErrorMessages
{
    /// <summary>
    /// The message a caller sees for an error that is not a business rule. The exception's own text,
    /// which can carry database messages or stack traces, is logged instead.
    /// </summary>
    public const string Unexpected = "An unexpected error occurred. If it persists, report it with the traceId.";

    /// <summary>
    /// The message recorded on a background run, such as a sync, that an unexpected error stopped. A run
    /// has no traceId to quote, so this points at the log entry written alongside it.
    /// </summary>
    public const string UnexpectedInRun = "An unexpected error stopped this run. The details are in the API log.";
}
