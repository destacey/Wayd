using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using NodaTime;

namespace Wayd.Common.Domain.Employees;

/// <summary>
/// Activation and deactivation arguments for employees, carrying the <see cref="EventActor"/> the state-change
/// events require.
/// </summary>
public sealed record EmployeeActivatableArgs : ActivatableArgs
{
    public required EventActor Actor { get; init; }

    public static EmployeeActivatableArgs Create(EventActor actor, Instant timestamp)
        => new() { Actor = actor, Timestamp = timestamp };
}
