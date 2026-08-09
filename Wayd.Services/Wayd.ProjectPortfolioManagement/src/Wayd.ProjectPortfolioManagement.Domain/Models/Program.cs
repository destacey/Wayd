using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

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

        Name = name;
        Description = description;

        AddDomainEvent(new ProgramDetailsUpdatedEvent(this, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Updates the program's timeline on behalf of an actor who must be authorized to manage it. Dates
    /// are gated because lifecycle guards read them — moving them changes which transitions are legal.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="dateRange">The new date range to assign to the program.</param>
    public Result UpdateTimeline(PpmActor actor, ProgramAncestryRoles ancestry, LocalDateRange? dateRange)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (Status is ProgramStatus.Active or ProgramStatus.Completed && dateRange is null)
        {
            return Result.Failure("An active and completed program must have a start and end date.");
        }

        DateRange = dateRange;

        return Result.Success();
    }

    /// <summary>
    /// Assigns an employee to a role on behalf of an actor who must be authorized to manage the program.
    /// Role assignment is gated because it is the path by which membership itself is granted — leaving it
    /// open would let any holder of the Update permission make themselves an Owner.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="role">The role to assign.</param>
    /// <param name="employeeId">The employee receiving the role.</param>
    public Result AssignRole(PpmActor actor, ProgramAncestryRoles ancestry, ProgramRole role, Guid employeeId)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        return RoleManager.AssignRole(_roles, Id, role, employeeId);
    }

    /// <summary>
    /// Removes an employee from a role on behalf of an actor who must be authorized to manage the program.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="role">The role to remove.</param>
    /// <param name="employeeId">The employee losing the role.</param>
    public Result RemoveRole(PpmActor actor, ProgramAncestryRoles ancestry, ProgramRole role, Guid employeeId)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        return RoleManager.RemoveAssignment(_roles, role, employeeId);
    }

    /// <summary>
    /// Replaces the program's role assignments on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    /// <param name="updatedRoles">The replacement role assignments.</param>
    public Result UpdateRoles(PpmActor actor, ProgramAncestryRoles ancestry, Dictionary<ProgramRole, HashSet<Guid>> updatedRoles)
    {
        if (!CanManageProgram(actor, ancestry))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        return RoleManager.UpdateRoles(_roles, Id, updatedRoles);
    }

    /// <summary>
    /// Associates a strategic theme with this program.
    /// </summary>
    public Result AddStrategicTheme(Guid strategicThemeId)
    {
        Guard.Against.NullOrEmpty(strategicThemeId, nameof(strategicThemeId));

        return StrategicThemeTagManager<Program>.AddStrategicThemeTag(_strategicThemeTags, Id, strategicThemeId, "program");
    }

    /// <summary>
    /// Removes a strategic theme from this program.
    /// </summary>
    public Result RemoveStrategicTheme(Guid strategicThemeId)
    {
        Guard.Against.NullOrEmpty(strategicThemeId, nameof(strategicThemeId));

        return StrategicThemeTagManager<Program>.RemoveStrategicThemeTag(_strategicThemeTags, strategicThemeId, "program");
    }

    /// <summary>
    /// Updates the strategic themes associated with this program.
    /// </summary>
    /// <param name="strategicThemeIds"></param>
    /// <returns></returns>
    public Result UpdateStrategicThemes(HashSet<Guid> strategicThemeIds)
    {
        Guard.Against.Null(strategicThemeIds, nameof(strategicThemeIds));

        return StrategicThemeTagManager<Program>.UpdateTags(_strategicThemeTags, Id, strategicThemeIds, "program");
    }

    #region Lifecycle

    /// <summary>
    /// Activates the program on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    public Result Activate(PpmActor actor, ProgramAncestryRoles ancestry)
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

        Status = ProgramStatus.Active;

        return Result.Success();
    }

    /// <summary>
    /// Marks the program as completed on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    public Result Complete(PpmActor actor, ProgramAncestryRoles ancestry)
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

        Status = ProgramStatus.Completed;

        return Result.Success();
    }

    /// <summary>
    /// Cancels the program on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="ancestry">Role assignments on the parent portfolio.</param>
    public Result Cancel(PpmActor actor, ProgramAncestryRoles ancestry)
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
        Status = ProgramStatus.Canceled;

        return Result.Success();
    }

    #endregion Lifecycle


    /// <summary>
    /// Adds an existing project to the program.
    /// </summary>
    internal Result AddProject(Project project)
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

        var result = project.UpdateProgram(this);
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
    internal Result RemoveProject(Project project)
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

        var result = project.UpdateProgram(null);
        if (result.IsFailure)
        {
            return result;
        }

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
    /// <param name="timestamp"></param>
    /// <returns></returns>
    internal static Program Create(string name, string description, LocalDateRange? dateRange, Guid portfolioId, Dictionary<ProgramRole, HashSet<Guid>>? roles, HashSet<Guid>? strategicThemes, Instant timestamp)
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
                timestamp
            )));

        return program;
    }
}
