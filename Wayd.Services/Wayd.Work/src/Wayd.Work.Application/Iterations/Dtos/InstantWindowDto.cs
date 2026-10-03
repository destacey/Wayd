namespace Wayd.Work.Application.Iterations.Dtos;

/// <summary>
/// The moments from <see cref="Earliest"/> to <see cref="Latest"/>, both included. A null
/// <see cref="Latest"/> means the window runs up to now, whenever the caller acts.
/// </summary>
public sealed record InstantWindowDto(Instant Earliest, Instant? Latest)
{
    public static InstantWindowDto From(InstantWindow window, Instant now) =>
        new(window.Earliest, window.Latest < now ? window.Latest : null);
}
