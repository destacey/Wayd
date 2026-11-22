namespace Wayd.Common.Application.Persistence;

/// <summary>
/// Recognises which database rule a failed save broke, for the catch that turns one specific, expected failure into
/// an answer and lets every other one propagate.
/// </summary>
/// <remarks>
/// Reads the provider exception's error number without naming its type: the application layers do not reference the
/// SQL Server client, whose <c>SqlException</c> carries the number as <c>Number</c>. Infrastructure's
/// <c>DbUpdateExceptionExtensions.IsUniqueViolation</c> reads the same numbers from the typed exception.
/// </remarks>
public static class DbUpdateErrors
{
    // SQL Server: 2601 = unique index, 2627 = unique constraint, 547 = foreign key (or check) constraint.
    private const int UniqueIndex = 2601;
    private const int UniqueConstraint = 2627;
    private const int ForeignKey = 547;

    /// <summary>Whether the save failed on a unique index or constraint.</summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        ErrorNumber(exception) is UniqueIndex or UniqueConstraint;

    /// <summary>Whether the save failed on a foreign key: the row at the other end was removed, or never existed.</summary>
    public static bool IsForeignKeyViolation(DbUpdateException exception) =>
        ErrorNumber(exception) is ForeignKey;

    private static int? ErrorNumber(DbUpdateException exception) =>
        exception.InnerException?.GetType().GetProperty("Number")?.GetValue(exception.InnerException) as int?;
}
