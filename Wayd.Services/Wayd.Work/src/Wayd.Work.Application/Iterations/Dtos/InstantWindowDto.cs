namespace Wayd.Work.Application.Iterations.Dtos;

/// <summary>
/// The moments from <c>earliest</c> to <c>latest</c>, both included. A null
/// <c>latest</c> means the window runs up to now, whenever the caller acts.
/// </summary>
public sealed record InstantWindowDto(Instant Earliest, Instant? Latest)
{
    public static InstantWindowDto From(InstantWindow window, Instant now) =>
        new(window.Earliest, window.Latest < now ? window.Latest : null);
}
