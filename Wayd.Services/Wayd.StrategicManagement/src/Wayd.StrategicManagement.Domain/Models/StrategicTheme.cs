using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.StrategicManagement.Domain.Models;

/// <summary>
/// Represents a high-level focus area or priority that guides related initiatives.
/// </summary>
public sealed class StrategicTheme : BaseAuditableEntity, IHasIdAndKey, IStrategicThemeData
{
    private StrategicTheme() { }

    private StrategicTheme(string name, string description, StrategicThemeState state)
    {
        Name = name;
        Description = description;
        State = state;
    }

    /// <summary>
    /// The unique key of the StrategicTheme.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the strategic theme, highlighting its focus or priority.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A detailed description of the strategic theme and its importance.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The current lifecycle state of the strategic theme (e.g., Active, Proposed, Archived).
    /// </summary>
    public StrategicThemeState State { get; private set; }

    /// <summary>
    /// Updates the Strategic Theme.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result Update(string name, string description, EventActor actor, Instant timestamp)
    {
        var previous = (Name, Description);

        Name = name;
        Description = description;

        // Compared after assignment because the setters trim.
        if ((Name, Description) == previous)
            return Result.Success();

        AddDomainEvent(new StrategicThemeUpdatedEvent(this, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Activates the Strategic Theme.
    /// </summary>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result Activate(EventActor actor, Instant timestamp)
    {
        if (State != StrategicThemeState.Proposed)
        {
            return Result.Failure("Only proposed strategic themes can be activated.");
        }

        State = StrategicThemeState.Active;
        AddDomainEvent(new StrategicThemeActivatedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Archives the Strategic Theme.
    /// </summary>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result Archive(EventActor actor, Instant timestamp)
    {
        if (State != StrategicThemeState.Active)
        {
            return Result.Failure("Only active strategic themes can be archived.");
        }

        State = StrategicThemeState.Archived;
        AddDomainEvent(new StrategicThemeArchivedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Indicates whether the Strategic Theme can be deleted.
    /// </summary>
    /// <returns></returns>
    public bool CanBeDeleted() => State == StrategicThemeState.Proposed;

    /// <summary>
    /// Creates a new Strategic Theme.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="state"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public static StrategicTheme Create(string name, string description, StrategicThemeState state, EventActor actor, Instant timestamp)
    {
        var theme = new StrategicTheme(name, description, state);

        // Captured now, not when the action runs: the event records the theme as created, so a caller
        // that changes it before the first save cannot rewrite the creation. Only Key waits for the save
        // that assigns it.
        var createdName = theme.Name;
        var createdDescription = theme.Description;
        var createdState = theme.State;

        theme.AddPostPersistenceAction(() => theme.AddDomainEvent(new StrategicThemeCreatedEvent(
            theme.Id,
            theme.Key,
            createdName,
            createdDescription,
            createdState,
            actor,
            timestamp)));

        return theme;
    }
}
