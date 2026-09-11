using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.Common.Domain.Events;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a program consisting of related projects within a portfolio, designed to achieve strategic objectives.
/// </summary>
public sealed class Program : BaseAuditableEntity, IHasIdAndKey, ISimpleProgram
{
    private const string UnauthorizedManageActorError =
        "You are not authorized to manage this program. Program or portfolio Owners and Managers may.";

    private readonly HashSet<RoleAssignment<ProgramRole>> _roles = [];
    private readonly HashSet<Project> _projects = [];
    private readonly HashSet<StrategicThemeTag<Program>> _strategicThemeTags = [];

    private Program() { }

    private Program(string name, string description, ProgramStatus status, LocalDateRange? dateRange, Guid portfolioId, Dictionary<ProgramRole, HashSet<Guid>>? roles = null, HashSet<Guid>? strategicThemes = null)
    {
        if (Status is ProgramStatus.Active or ProgramStatus.Completed && dateRange is null)
        {
            throw new InvalidOperationException("An active and completed program must have a start and end date.");
        }

        Name = name;
        Description = description;
        Status = status;
        PortfolioId = portfolioId;
        DateRange = dateRange;

        _roles = roles?
            .SelectMany(r => r.Value
                .Select(e => new RoleAssignment<ProgramRole>(Id, r.Key, e)))
            .ToHashSet()
            ?? [];

        _strategicThemeTags = strategicThemes?.Select(t => new StrategicThemeTag<Program>(Id, t)).ToHashSet()
            ?? [];
    }

    /// <summary>
    /// The unique key of the program. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the program.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A detailed description of the program's purpose and scope.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The current status of the program.
    /// </summary>
    public ProgramStatus Status { get; private set; }

    /// <summary>
    /// The roles associated with this program.
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<ProgramRole>> Roles => _roles;

    /// <summary>
    /// The date range defining the program's lifecycle.
    /// </summary>
    public LocalDateRange? DateRange { get; private set; }

    /// <summary>
    /// The Id of the portfolio to which this program belongs.
    /// </summary>
    public Guid PortfolioId { get; private set; }

    /// <summary>
    /// The portfolio to which this program belongs.
    /// </summary>
    public ProjectPortfolio? Portfolio { get; private set; }

    /// <summary>
    /// The projects associated with this program.
    /// </summary>
    public IReadOnlyCollection<Project> Projects => _projects;

    /// <summary>
    /// Indicates if the program is currently accepting new projects.
    /// </summary>
    public bool AcceptingProjects => Status == ProgramStatus.Active;

    /// <summary>
    /// Indicates if the project is in a closed state.
    /// </summary>
    public bool IsClosed => Status is ProgramStatus.Completed or ProgramStatus.Canceled;

    /// <summary>
    /// The strategic themes associated with this program.
    /// </summary>
    public IReadOnlyCollection<StrategicThemeTag<Program>> StrategicThemeTags => _strategicThemeTags;

    /// <summary>
    /// Indicates whether the program can be deleted.
    /// </summary>
    /// <returns></returns>
    public bool CanBeDeleted() => Status is ProgramStatus.Proposed;

    /// <summary>
    /// Read-side authorization predicate: returns true if the given actor may manage this program.
    /// Owner/Manager on the program itself OR on the parent portfolio is sufficient, as is the
    /// domain-wide PPM administrator grant. Sponsors are intentionally excluded — they fund and
    /// oversee but don't run delivery, matching <see cref="Project.CanManageProject(PpmActor, ProjectAncestryRoles)"/>.
    ///
    /// The aggregate's management methods enforce the same rule inline, so callers cannot bypass it;
    /// this method also lets the API layer surface the decision to the UI for action-availability hints.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <returns>True if the actor may manage the program; otherwise, false.</returns>
    public bool CanManageProgram(PpmActor actor, ProgramAncestryRoles ancestry)
    {
        Guard.Against.Null(actor, nameof(actor));
        Guard.Against.Null(ancestry, nameof(ancestry));

        if (actor.IsPpmAdministrator)
            return true;

        if (_roles.Any(r => r.EmployeeId == actor.EmployeeId && r.Role is ProgramRole.Owner or ProgramRole.Manager))
            return true;

        if (ancestry.PortfolioRoles.Any(r => r.EmployeeId == actor.EmployeeId && r.Role is ProjectPortfolioRole.Owner or ProjectPortfolioRole.Manager))
            return true;

        return false;
    }

    /// <summary>
    /// Updates the program's details on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="name">The new name to assign to the program. Cannot be null.</param>
    /// <param name="description">The new description to assign to the program. Cannot be null.</param>
    /// <param name="timestamp">The timestamp indicating when the update occurred.</param>
    public Result UpdateDetails(PpmActor actor, ProgramAncestryRoles ancestry, string name, string description, Instant timestamp)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        // Compared after assignment, never against the arguments: the setters normalise, so a caller
        // passing "Platform " where "Platform" is stored has changed nothing.
        var before = (Name, Description);

        Name = name;
        Description = description;

        if (before != (Name, Description))
        {
            AddDomainEvent(new ProgramDetailsUpdatedEvent(this, actor.ToEventActor(), timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the program's timeline on behalf of an actor who must be authorized to manage it. Dates
    /// are gated because lifecycle guards read them — moving them changes which transitions are legal.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="dateRange">The new date range to assign to the program.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result UpdateTimeline(PpmActor actor, ProgramAncestryRoles ancestry, LocalDateRange? dateRange, Instant timestamp)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (Status is ProgramStatus.Active or ProgramStatus.Completed && dateRange is null)
        {
            return Result.Failure("An active and completed program must have a start and end date.");
        }

        if (Equals(DateRange, dateRange))
        {
            return Result.Success();
        }

        DateRange = dateRange;

        AddDomainEvent(new ProgramTimelineChangedEvent(Id, Key, DateRange, actor.ToEventActor(), timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Replaces the program's role assignments on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="updatedRoles">The replacement role assignments.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result UpdateRoles(PpmActor actor, ProgramAncestryRoles ancestry, Dictionary<ProgramRole, HashSet<Guid>> updatedRoles, Instant timestamp)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        var before = RoleMap();

        var result = RoleManager.UpdateRoles(_roles, Id, updatedRoles);
        if (result.IsFailure)
        {
            return result;
        }

        var after = RoleMap();
        var (added, removed) = RoleManager.Diff(before, after);
        if (added.Length > 0 || removed.Length > 0)
        {
            AddDomainEvent(new ProgramRolesChangedEvent(Id, Key, added, removed, after, actor.ToEventActor(), timestamp));
        }

        return result;
    }

    private Dictionary<int, Guid[]> RoleMap() => RoleManager.ToRoleMap(_roles);

    /// <summary>
    /// Associates a strategic theme with this program.
    /// </summary>
    public Result AddStrategicTheme(Guid strategicThemeId, PpmActor actor, Instant timestamp)
    {
        Guard.Against.NullOrEmpty(strategicThemeId, nameof(strategicThemeId));

        var result = StrategicThemeTagManager<Program>.AddStrategicThemeTag(_strategicThemeTags, Id, strategicThemeId, "program");
        if (result.IsSuccess)
        {
            RaiseStrategicThemesChanged(actor, timestamp);
        }

        return result;
    }

    /// <summary>
    /// Removes a strategic theme from this program.
    /// </summary>
    public Result RemoveStrategicTheme(Guid strategicThemeId, PpmActor actor, Instant timestamp)
    {
        Guard.Against.NullOrEmpty(strategicThemeId, nameof(strategicThemeId));

        var result = StrategicThemeTagManager<Program>.RemoveStrategicThemeTag(_strategicThemeTags, strategicThemeId, "program");
        if (result.IsSuccess)
        {
            RaiseStrategicThemesChanged(actor, timestamp);
        }

        return result;
    }

    /// <summary>
    /// Updates the strategic themes associated with this program.
    /// </summary>
    /// <param name="strategicThemeIds"></param>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    /// <returns></returns>
    public Result UpdateStrategicThemes(HashSet<Guid> strategicThemeIds, PpmActor actor, Instant timestamp)
    {
        Guard.Against.Null(strategicThemeIds, nameof(strategicThemeIds));

        var before = _strategicThemeTags.Select(x => x.StrategicThemeId).ToHashSet();

        var result = StrategicThemeTagManager<Program>.UpdateTags(_strategicThemeTags, Id, strategicThemeIds, "program");

        // The update command replaces the tag set on every save, so raising unconditionally would report
        // a change on edits that never touched the themes.
        if (result.IsSuccess && !before.SetEquals(_strategicThemeTags.Select(x => x.StrategicThemeId)))
        {
            RaiseStrategicThemesChanged(actor, timestamp);
        }

        return result;
    }

    private void RaiseStrategicThemesChanged(PpmActor actor, Instant timestamp) =>
        AddDomainEvent(new ProgramStrategicThemesChangedEvent(
            Id,
            Key,
            [.. _strategicThemeTags.Select(x => x.StrategicThemeId)],
            actor.ToEventActor(),
            timestamp));

    #region Lifecycle

    /// <summary>
    /// Activates the program on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="timestamp">The timestamp indicating when the transition occurred.</param>
    public Result Activate(PpmActor actor, ProgramAncestryRoles ancestry, Instant timestamp)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (Status != ProgramStatus.Proposed)
        {
            return Result.Failure("Only proposed programs can be activated.");
        }

        if (DateRange is null)
        {
            return Result.Failure("The program must have a start and end date before it can be activated.");
        }

        ChangeStatus(ProgramStatus.Active, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Marks the program as completed on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="timestamp">The timestamp indicating when the transition occurred.</param>
    public Result Complete(PpmActor actor, ProgramAncestryRoles ancestry, Instant timestamp)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (Status != ProgramStatus.Active)
        {
            return Result.Failure("Only active programs can be completed.");
        }

        if (DateRange is null)
        {
            return Result.Failure("The program must have a start and end date before it can be completed.");
        }

        if (_projects.Any(p => !p.IsClosed))
        {
            return Result.Failure("All projects must be completed or canceled before the program can be completed.");
        }

        ChangeStatus(ProgramStatus.Completed, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Cancels the program on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="timestamp">The timestamp indicating when the transition occurred.</param>
    public Result Cancel(PpmActor actor, ProgramAncestryRoles ancestry, Instant timestamp)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (Status is ProgramStatus.Completed or ProgramStatus.Canceled)
        {
            return Result.Failure("The program is already completed or canceled.");
        }

        if (Status is ProgramStatus.Active)
        {
            if (_projects.Any(p => !p.IsClosed))
            {
                return Result.Failure("All projects must be completed or canceled before the program can be canceled.");
            }
        }

        // Directly allow Proposed → Canceled without setting DateRange
        ChangeStatus(ProgramStatus.Canceled, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Moves the program to <paramref name="toStatus"/> and records the transition.
    /// </summary>
    /// <remarks>
    /// A program keeps no status history, so this event is the whole record of the move.
    /// </remarks>
    private void ChangeStatus(ProgramStatus toStatus, PpmActor actor, Instant timestamp)
    {
        var fromStatus = Status;

        Status = toStatus;

        AddDomainEvent(new ProgramStatusChangedEvent(
            Id,
            Key,
            fromStatus.ToString(),
            LifecycleCategories<ProgramStatus>.Of(fromStatus),
            toStatus.ToString(),
            LifecycleCategories<ProgramStatus>.Of(toStatus),
            actor.ToEventActor(),
            timestamp));
    }

    #endregion Lifecycle


    /// <summary>
    /// Adds an existing project to the program.
    /// </summary>
    internal Result AddProject(Project project, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(project, nameof(project));

        if (AcceptingProjects is false)
        {
            return Result.Failure("The program is not accepting new projects.");
        }

        if (project.PortfolioId != PortfolioId)
        {
            return Result.Failure("The project must belong to the same portfolio as the program.");
        }

        if (_projects.Contains(project))
        {
            return Result.Failure("The project is already part of this program.");
        }

        var result = project.UpdateProgram(this, actor, timestamp);
        if (result.IsFailure)
        {
            return result;
        }

        _projects.Add(project);

        return Result.Success();
    }

    /// <summary>
    /// Removes an existing project from the program.
    /// </summary>
    internal Result RemoveProject(Project project, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(project, nameof(project));

        if (!_projects.Contains(project))
        {
            return Result.Failure("The project is not part of this program.");
        }

        if (IsClosed)
        {
            return Result.Failure("Projects cannot be removed from a closed program.");
        }

        var result = project.UpdateProgram(null, actor, timestamp);
        if (result.IsFailure)
        {
            return result;
        }

        _projects.Remove(project);

        return Result.Success();
    }

    /// <summary>
    /// Removes a project that is being deleted, skipping the reparenting the ordinary removal announces.
    /// Separate from <see cref="RemoveProject"/> rather than a flag on it, so the two intents read
    /// differently at the call site.
    /// </summary>
    internal Result DetachProjectForDeletion(Project project)
    {
        Guard.Against.Null(project, nameof(project));

        if (!_projects.Contains(project))
        {
            return Result.Failure("The project is not part of this program.");
        }

        if (IsClosed)
        {
            return Result.Failure("Projects cannot be removed from a closed program.");
        }

        project.ClearProgramForDeletion();
        _projects.Remove(project);

        return Result.Success();
    }

    /// <summary>
    /// Checks if the program is active on the specified date.
    /// </summary>
    public bool IsActiveOn(LocalDate date)
    {
        Guard.Against.Null(date, nameof(date));

        return DateRange is not null && DateRange.IsActiveOn(date);
    }

    /// <summary>
    /// Creates a new program with the specified details.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="dateRange"></param>
    /// <param name="portfolioId"></param>
    /// <param name="roles"></param>
    /// <param name="strategicThemes"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    internal static Program Create(string name, string description, LocalDateRange? dateRange, Guid portfolioId, Dictionary<ProgramRole, HashSet<Guid>>? roles, HashSet<Guid>? strategicThemes, EventActor actor, Instant timestamp)
    {
        var program = new Program(name, description, ProgramStatus.Proposed, dateRange, portfolioId, roles, strategicThemes);

        program.AddPostPersistenceAction(() => program.AddDomainEvent(new ProgramCreatedEvent(
                program,
                (int)program.Status,
                program.DateRange,
                program.PortfolioId,
                program.Roles
                    .GroupBy(x => (int)x.Role)
                    .ToDictionary(x => x.Key, x => x.Select(y => y.EmployeeId).ToArray()),
                [.. program.StrategicThemeTags.Select(x => x.StrategicThemeId)],
                actor,
                timestamp
            )));

        return program;
    }
}
