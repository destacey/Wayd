using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>A point on a sprint's ideal burn-down.</summary>
public sealed record SprintIdealPointDto
{
    /// <summary>When the point falls.</summary>
    public Instant At { get; init; }

    /// <summary>
    /// The share of the committed work still expected to remain: 1 at the commitment point, 0 at the effective
    /// end. Multiply by the committed work in either unit.
    /// </summary>
    public double Remaining { get; init; }

    /// <summary>The API shape of a domain <see cref="SprintIdealPoint"/>.</summary>
    public static SprintIdealPointDto From(SprintIdealPoint point) => new() { At = point.At, Remaining = point.Remaining };
}
