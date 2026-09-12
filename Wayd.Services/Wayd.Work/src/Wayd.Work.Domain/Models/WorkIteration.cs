using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.WorkManagement.WorkIterations;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Interfaces;
using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// A copy of the Wayd.Common.Domain.Interfaces.Planning.Iterations.ISimpleIteration interface.  Used to hold basic iteration information for the work service and db context.
/// </summary>
public sealed class WorkIteration : BaseEntity<Guid>, ISimpleIteration, IHasIdAndKey, IHasOptionalWorkTeam
{
    private WorkIteration() { }

    public WorkIteration(ISimpleIteration iteration)
    {
        Id = iteration.Id;
        Key = iteration.Key;
        Name = iteration.Name;
        Type = iteration.Type;
        State = iteration.State;
        DateRange = iteration.DateRange;
        TeamId = iteration.TeamId;
    }

    public int Key { get; private init; }
    public string Name { get; private set; } = default!;
    public IterationType Type { get; private set; }
    public IterationState State { get; private set; }
    public IterationDateRange DateRange { get; private set; } = default!;
    public Guid? TeamId { get; private set; }
    public WorkTeam? Team { get; private set; }

    /// <summary>
    /// Updates the current iteration with the values from the specified iteration.
    /// </summary>
    /// <param name="iteration">The iteration containing the updated values. The <see cref="ISimpleIteration.Id"/> must match the current
    /// iteration's ID.</param>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="iteration"/> ID does not match the current iteration's ID.</exception>
    public Result Update(ISimpleIteration iteration, EventActor actor, Instant timestamp)
    {
        if (iteration.Id != Id)
        {
            return Result.Failure("Iteration ID does not match.");
        }

        var previous = (Name, Type, State, DateRange, TeamId);

        Name = iteration.Name;
        Type = iteration.Type;
        State = iteration.State;
        DateRange = iteration.DateRange;
        TeamId = iteration.TeamId;

        if ((Name, Type, State, DateRange, TeamId) == previous)
        {
            return Result.Success();
        }

        AddDomainEvent(new WorkIterationUpdatedEvent(this, actor, timestamp));

        return Result.Success();
    }
}
