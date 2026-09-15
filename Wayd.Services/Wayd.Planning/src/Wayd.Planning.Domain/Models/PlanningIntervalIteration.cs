using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces;
using Wayd.Planning.Domain.Interfaces;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

public sealed class PlanningIntervalIteration : BaseSoftDeletableEntity, ILocalSchedule, INavigable
{
    private readonly List<PlanningIntervalIterationSprint> _iterationSprints = [];

    private PlanningIntervalIteration() { }

    internal PlanningIntervalIteration(Guid planningIntervalId, string name, IterationCategory category, LocalDateRange dateRange)
    {
        // TODO guard against default planningIntervalId, but PI would need to generate a new Guid, rather than depend on the DB for this to work.
        PlanningIntervalId = planningIntervalId;
        Name = name;
        Category = category;
        DateRange = dateRange;
    }

    /// <summary>
    /// The alternate key of the Planning Interval Iteration.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The ID of the Planning Interval this iteration belongs to.
    /// </summary>
    public Guid PlanningIntervalId { get; private init; }

    /// <summary>
    /// The Planning Interval this iteration belongs to.
    /// </summary>
    public PlanningInterval? PlanningInterval { get; private set; }

    /// <summary>
    /// The name of the Planning Interval.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The category of the Planning Interval Iteration.
    /// </summary>
    public IterationCategory Category { get; private set; }

    /// <summary>
    /// The date range of the Planning Interval Iteration.
    /// </summary>
    public LocalDateRange DateRange
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(DateRange));
    } = default!;

    /// <summary>
    /// Gets the sprints mapped to this iteration.
    /// </summary>
    public IReadOnlyCollection<PlanningIntervalIterationSprint> IterationSprints => _iterationSprints.AsReadOnly();

    /// <summary>Iteration state on given date.</summary>
    /// <param name="date">The date.</param>
    /// <returns></returns>
    public IterationState StateOn(LocalDate date)
    {
        if (DateRange.IsPastOn(date)) { return IterationState.Completed; }
        if (DateRange.IsActiveOn(date)) { return IterationState.Active; }
        return IterationState.Future;
    }

    /// <summary>
    /// Updates the Planning Interval Iteration.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="category"></param>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    internal Result Update(string name, IterationCategory category, LocalDateRange dateRange)
    {
        Name = name;
        Category = category;
        DateRange = dateRange;

        return Result.Success();
    }

    /// <summary>
    /// Factory method to create a Planning Interval Iteration.
    /// </summary>
    /// <param name="planningIntervalId"></param>
    /// <param name="name"></param>
    /// <param name="category"></param>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    internal static PlanningIntervalIteration Create(Guid planningIntervalId, string name, IterationCategory category, LocalDateRange dateRange)
    {
        return new PlanningIntervalIteration(planningIntervalId, name, category, dateRange);
    }
}
