using NodaTime;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// Which days of a sprint the team works: a day in its working week that is neither a holiday in its calendar
/// nor one of the sprint's team days off. A sprint's ideal burn-down falls only on working days.
/// </summary>
/// <remarks>
/// Each day weighs 1 when worked and 0 when not, and the ideal line falls in proportion to the weight. A weight
/// is a share of the team working that day, so a later per-person calendar can weigh a day between the two
/// without changing how the line is drawn.
/// </remarks>
public sealed class SprintWorkingDays
{
    private readonly WorkingWeek _workingWeek;
    private readonly HashSet<LocalDate> _daysOff;

    public SprintWorkingDays(WorkingWeek workingWeek, IEnumerable<LocalDate> daysOff)
    {
        _workingWeek = workingWeek;
        _daysOff = [.. daysOff];
    }

    /// <summary>Every day worked: no weekends, holidays or days off.</summary>
    public static SprintWorkingDays EveryDay { get; } = new(
        WorkingWeek.Create([
            IsoDayOfWeek.Monday,
            IsoDayOfWeek.Tuesday,
            IsoDayOfWeek.Wednesday,
            IsoDayOfWeek.Thursday,
            IsoDayOfWeek.Friday,
            IsoDayOfWeek.Saturday,
            IsoDayOfWeek.Sunday,
        ]).Value,
        []);

    /// <summary>How much of a working day <paramref name="day"/> is: 1 when the team works it, 0 when it does not.</summary>
    public double Weight(LocalDate day) => _workingWeek.Includes(day) && !_daysOff.Contains(day) ? 1 : 0;
}
