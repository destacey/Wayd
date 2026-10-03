using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>The moments from <see cref="Earliest"/> to <see cref="Latest"/>, both included.</summary>
public sealed record InstantWindow(Instant Earliest, Instant Latest)
{
    public bool Contains(Instant instant) => instant >= Earliest && instant <= Latest;
}
