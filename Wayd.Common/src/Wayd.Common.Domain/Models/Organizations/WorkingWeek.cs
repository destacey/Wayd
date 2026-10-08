using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Common.Domain.Models.Organizations;

/// <summary>
/// The days of the week a team works. A sprint's ideal burn-down drops only on these days.
/// </summary>
/// <remarks>
/// Always holds at least one day. Persisted as its <see cref="ToString"/> form, the day names in week order
/// separated by commas, and read back with <see cref="Parse"/>.
/// </remarks>
public sealed class WorkingWeek : IEquatable<WorkingWeek>
{
    private const char Separator = ',';

    private WorkingWeek(IEnumerable<IsoDayOfWeek> days)
    {
        Days = [.. days.Distinct().Order()];
    }

    /// <summary>Monday to Friday: the default for a new operating model.</summary>
    public static WorkingWeek MondayToFriday { get; } = new([
        IsoDayOfWeek.Monday,
        IsoDayOfWeek.Tuesday,
        IsoDayOfWeek.Wednesday,
        IsoDayOfWeek.Thursday,
        IsoDayOfWeek.Friday,
    ]);

    /// <summary>The working days, Monday first.</summary>
    public IReadOnlyList<IsoDayOfWeek> Days { get; }

    /// <summary>Whether <paramref name="date"/> falls on a working day of the week.</summary>
    public bool Includes(LocalDate date) => Days.Contains(date.DayOfWeek);

    /// <summary>
    /// A working week of <paramref name="days"/>, ignoring repeats. Fails when there are none or one is not a
    /// day of the week.
    /// </summary>
    public static Result<WorkingWeek> Create(IEnumerable<IsoDayOfWeek>? days)
    {
        var list = days?.ToList() ?? [];
        if (list.Count == 0)
            return Result.Failure<WorkingWeek>("A working week needs at least one working day.");

        if (list.Any(d => d < IsoDayOfWeek.Monday || d > IsoDayOfWeek.Sunday))
            return Result.Failure<WorkingWeek>("A working day must be a day of the week.");

        return new WorkingWeek(list);
    }

    /// <summary>Reads a working week back from its <see cref="ToString"/> form.</summary>
    /// <exception cref="FormatException">The value is not a list of day names.</exception>
    public static WorkingWeek Parse(string value)
    {
        var days = value
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => Enum.TryParse<IsoDayOfWeek>(d, out var day)
                ? day
                : throw new FormatException($"'{d}' is not a day of the week."));

        var result = Create(days);
        return result.IsSuccess ? result.Value : throw new FormatException(result.Error);
    }

    /// <summary>The day names in week order, separated by commas: <c>Monday,Tuesday</c>.</summary>
    public override string ToString() => string.Join(Separator, Days);

    public bool Equals(WorkingWeek? other) => other is not null && Days.SequenceEqual(other.Days);

    public override bool Equals(object? obj) => Equals(obj as WorkingWeek);

    public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);

    public static bool operator ==(WorkingWeek? left, WorkingWeek? right) => Equals(left, right);

    public static bool operator !=(WorkingWeek? left, WorkingWeek? right) => !Equals(left, right);
}
