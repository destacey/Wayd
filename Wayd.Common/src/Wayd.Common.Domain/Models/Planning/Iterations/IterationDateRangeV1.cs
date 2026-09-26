using NodaTime;

namespace Wayd.Common.Domain.Models.Planning.Iterations;

/// <summary>
/// The shape the first generation of the iteration events carried an iteration's dates in: instants, with
/// synced dates at midnight UTC and <see cref="End"/> the start of the last day.
/// </summary>
/// <remarks>
/// Frozen, and read only by the superseded events that carry it. Its members are the shape their payloads
/// were written in, so neither may change.
/// </remarks>
[Obsolete("Superseded by IterationDateRange. Kept only to deserialize payloads already written in this shape.")]
public sealed record IterationDateRangeV1
{
    public IterationDateRangeV1(Instant? start, Instant? end)
    {
        Start = start;
        End = end;
    }

    public Instant? Start { get; }

    public Instant? End { get; }

    /// <summary>
    /// The calendar days these instants stood for: the UTC date of each, as the data migration took them.
    /// </summary>
    public IterationDateRange ToIterationDateRange()
        => new(Start?.InUtc().Date, End?.InUtc().Date);
}
