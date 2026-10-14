namespace Wayd.Work.Domain.Models.SprintCapacity;

/// <summary>What a completed sprint delivered and the delivery capacity it had, in the unit being compared.</summary>
/// <param name="Velocity">What the sprint completed, by count or in the estimate being compared.</param>
/// <param name="DeliveryPersonDays">The team's delivery capacity over the sprint's planned dates.</param>
public sealed record PriorSprintCapacity(double Velocity, decimal DeliveryPersonDays);

/// <summary>
/// A sprint's scope set against what its delivery capacity is expected to deliver: the team's recent velocity
/// scaled by how this sprint's capacity compares with the capacity those sprints had.
/// </summary>
/// <param name="Outcome">Whether the comparison could be made, and if not, why.</param>
/// <param name="ExpectedScope">
/// What the capacity is expected to deliver, in the scope's unit; null unless <see cref="Outcome"/> is
/// <see cref="SprintCapacityOutcome.Compared"/>.
/// </param>
/// <param name="PriorSprints">How many completed sprints the expectation was drawn from.</param>
/// <param name="IsOverCapacity">Whether the scope is more than the capacity is expected to deliver, on a sprint not yet completed.</param>
public sealed record SprintCapacityComparison(SprintCapacityOutcome Outcome, double? ExpectedScope, int PriorSprints, bool IsOverCapacity)
{
    /// <summary>
    /// Compares <paramref name="scope"/> with what <paramref name="deliveryPersonDays"/> is expected to deliver:
    /// the velocity of the <paramref name="priorSprints"/> that had delivery capacity, per person-day of it,
    /// times this sprint's. That is their average velocity scaled by this sprint's capacity over their average.
    /// </summary>
    /// <param name="isCompleted">
    /// Whether the sprint has ended. A completed sprint is compared but never warned about: the warning is for
    /// planning, and its scope can no longer change.
    /// </param>
    public static SprintCapacityComparison Compare(
        double scope,
        decimal deliveryPersonDays,
        IReadOnlyCollection<PriorSprintCapacity> priorSprints,
        bool isCompleted)
    {
        if (deliveryPersonDays <= 0)
            return new SprintCapacityComparison(SprintCapacityOutcome.NoCapacity, null, 0, false);

        if (priorSprints.Count == 0)
            return new SprintCapacityComparison(SprintCapacityOutcome.NoPriorSprints, null, 0, false);

        // A sprint nobody was allocated to says nothing about what a person-day delivers.
        List<PriorSprintCapacity> measured = [.. priorSprints.Where(s => s.DeliveryPersonDays > 0)];
        if (measured.Count == 0)
            return new SprintCapacityComparison(SprintCapacityOutcome.NoPriorCapacity, null, 0, false);

        var perPersonDay = measured.Sum(s => s.Velocity) / (double)measured.Sum(s => s.DeliveryPersonDays);
        var expected = perPersonDay * (double)deliveryPersonDays;

        return new SprintCapacityComparison(SprintCapacityOutcome.Compared, expected, measured.Count, !isCompleted && scope > expected);
    }
}
