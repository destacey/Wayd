using NodaTime;
using Wayd.Common.Domain.Employees;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

/// <summary>
/// A denormalised, point-in-time summary of a project's most recent score, kept on the
/// <see cref="Project"/> so list/grid views can show the score, when it was set, who set it, and which
/// model produced it without joining to the score history. It is a cache of the latest
/// <see cref="ProjectScore"/> — the authoritative record remains in the score history — and is only ever
/// written by <see cref="Project.RecordScore"/>, so the two cannot disagree.
/// </summary>
/// <remarks>
/// Unlike <see cref="ProjectScore"/> (a historical snapshot that deliberately freezes the model name),
/// this is a view of the <em>current</em> latest score. The scorer is referenced by id only
/// (<see cref="ScoredById"/>) so a renamed employee always reads correctly — the name is resolved by a
/// join at projection time, not frozen here.
/// </remarks>
public sealed class ScoreSummary
{
    private ScoreSummary() { }

    internal ScoreSummary(
        decimal value,
        Instant scoredOn,
        Guid scoredById,
        string scoringModelName)
    {
        Value = value;
        ScoredOn = scoredOn;
        ScoredById = scoredById;
        ScoringModelName = scoringModelName;
    }

    /// <summary>
    /// The primary score value of the most recent score.
    /// </summary>
    public decimal Value { get; private init; }

    /// <summary>
    /// When the most recent score was recorded.
    /// </summary>
    public Instant ScoredOn { get; private init; }

    /// <summary>
    /// The ID of the employee who recorded the most recent score. The display name is resolved through
    /// the <see cref="ScoredBy"/> navigation so it always reflects the employee's current name.
    /// </summary>
    public Guid ScoredById { get; private init; }

    /// <summary>
    /// The employee who recorded the most recent score, resolved by navigation (not frozen).
    /// </summary>
    public Employee? ScoredBy { get; private set; }

    /// <summary>
    /// The name of the scoring model that produced the most recent score, frozen at scoring time.
    /// </summary>
    public string ScoringModelName { get; private init; } = default!;
}
