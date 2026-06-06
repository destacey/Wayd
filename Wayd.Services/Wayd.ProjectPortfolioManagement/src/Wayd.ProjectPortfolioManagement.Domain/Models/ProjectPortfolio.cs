using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a collection of projects or programs that are managed together to achieve strategic results.
/// </summary>
public sealed class ProjectPortfolio : BaseAuditableEntity, IHasIdAndKey
{
    private const string ReadOnlyErrorMessage = "Project Portfolio is readonly and cannot be updated.";

    private readonly HashSet<RoleAssignment<ProjectPortfolioRole>> _roles = [];
    private readonly HashSet<Program> _programs = [];
    private readonly HashSet<Project> _projects = [];
    private readonly HashSet<StrategicInitiative> _strategicInitiatives = [];

    private ProjectPortfolio() { }

    private ProjectPortfolio(string name, string description, ProjectPortfolioStatus status, Dictionary<ProjectPortfolioRole, HashSet<Guid>>? roles = null, FlexibleDateRange? dateRange = null)
    {
        if (status is ProjectPortfolioStatus.Active or ProjectPortfolioStatus.OnHold && dateRange?.Start is null)
        {
            throw new InvalidOperationException("An active or on hold portfolio must have a start date.");
        }

        if (status is ProjectPortfolioStatus.Closed or ProjectPortfolioStatus.Archived && (dateRange?.Start is null || dateRange?.End is null))
        {
            throw new InvalidOperationException("A closed or archived portfolio must have a start and end date.");
        }

        Name = name;
        Description = description;
        Status = status;
        DateRange = dateRange;

        _roles = roles?
            .SelectMany(r => r.Value
                .Select(e => new RoleAssignment<ProjectPortfolioRole>(Id, r.Key, e)))
            .ToHashSet()
            ?? [];
    }

    /// <summary>
    /// The unique key of the portfolio.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the portfolio.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A detailed description of the portfolio’s purpose.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The status of the portfolio.
    /// </summary>
    public ProjectPortfolioStatus Status { get; private set; }

    /// <summary>
    /// The roles associated with the portfolio.
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<ProjectPortfolioRole>> Roles => _roles;

    /// <summary>
    /// The date range defining the portfolio’s lifecycle.
    /// </summary>
    public FlexibleDateRange? DateRange { get; private set; }

    /// <summary>
    /// The programs associated with this portfolio.
    /// </summary>
    public IReadOnlyCollection<Program> Programs => _programs;

    /// <summary>
    /// The projects associated with this portfolio.
    /// </summary>
    public IReadOnlyCollection<Project> Projects => _projects;

    /// <summary>
    /// The strategic initiatives associated with this portfolio.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiative> StrategicInitiatives => _strategicInitiatives;

    /// <summary>
    /// The ID of the scoring model the portfolio's projects are scored with. Null when scoring is not
    /// enabled for the portfolio. Assigning a model enables project scoring across the portfolio.
    /// </summary>
    public Guid? ScoringModelId { get; private set; }

    /// <summary>
    /// The scoring model assigned to the portfolio, if any.
    /// </summary>
    public ScoringModel? ScoringModel { get; private set; }

    /// <summary>
    /// Indicates whether the portfolio is readonly.
    /// </summary>
    public bool IsActive => Status is ProjectPortfolioStatus.Active or ProjectPortfolioStatus.OnHold;

    /// <summary>
    /// Indicates whether the portfolio is readonly.
    /// </summary>
    public bool IsReadOnly => Status is ProjectPortfolioStatus.Archived;

    /// <summary>
    /// Indicates whether the portfolio can be deleted.
    /// </summary>
    /// <returns></returns>
    public bool CanBeDeleted() => Status is ProjectPortfolioStatus.Proposed;

    /// <summary>
    /// Updates the portfolio details, including the date range.
    /// </summary>
    public Result UpdateDetails(string name, string description)
    {
        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        Name = name;
        Description = description;

        return Result.Success();
    }

    /// <summary>
    /// Assigns an employee to a specific role within the portfolio, allowing multiple employees per role.
    /// </summary>
    public Result AssignRole(ProjectPortfolioRole role, Guid employeeId)
    {
        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        return RoleManager.AssignRole(_roles, Id, role, employeeId);
    }

    /// <summary>
    /// Removes an employee from a specific role.
    /// </summary>
    public Result RemoveRole(ProjectPortfolioRole role, Guid employeeId)
    {
        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        return RoleManager.RemoveAssignment(_roles, role, employeeId);
    }

    /// <summary>
    /// Updates the roles for the portfolio.
    /// </summary>
    /// <param name="updatedRoles"></param>
    /// <returns></returns>
    public Result UpdateRoles(Dictionary<ProjectPortfolioRole, HashSet<Guid>> updatedRoles)
    {
        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        return RoleManager.UpdateRoles(_roles, Id, updatedRoles);
    }

    #region Scoring

    /// <summary>
    /// Assigns an active scoring model to the portfolio, enabling its projects to be scored. Existing
    /// project scores are unaffected — they retain their own frozen model reference.
    /// </summary>
    /// <param name="model">The scoring model to assign. Must be in the Active state.</param>
    public Result AssignScoringModel(ScoringModel model)
    {
        Guard.Against.Null(model, nameof(model));

        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        if (model.State != ScoringModelState.Active)
        {
            return Result.Failure("Only active scoring models can be assigned to a portfolio.");
        }

        ScoringModelId = model.Id;

        return Result.Success();
    }

    /// <summary>
    /// Clears the portfolio's assigned scoring model, disabling new project scoring. Existing project
    /// scores are unaffected.
    /// </summary>
    public Result ClearScoringModel()
    {
        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        ScoringModelId = null;

        return Result.Success();
    }

    #endregion Scoring

    #region Lifecycle

    /// <summary>
    /// Activates the portfolio on the specified start date.
    /// </summary>
    /// <param name="startDate"></param>
    /// <returns></returns>
    public Result Activate(LocalDate startDate)
    {
        Guard.Against.Null(startDate, nameof(startDate));

        if (Status != ProjectPortfolioStatus.Proposed)
        {
            return Result.Failure("Only proposed portfolios can be activated.");
        }

        Status = ProjectPortfolioStatus.Active;
        DateRange = new FlexibleDateRange(startDate);

        return Result.Success();
    }

    /// <summary>
    /// Puts the portfolio on hold.
    /// </summary>
    public Result Pause()
    {
        if (Status != ProjectPortfolioStatus.Active)
        {
            return Result.Failure("Only active portfolios can be put on hold.");
        }

        Status = ProjectPortfolioStatus.OnHold;

        return Result.Success();
    }

    /// <summary>
    /// Resumes an on-hold portfolio.
    /// </summary>
    public Result Resume()
    {
        if (Status != ProjectPortfolioStatus.OnHold)
        {
            return Result.Failure("Only portfolios on hold can be resumed.");
        }

        Status = ProjectPortfolioStatus.Active;

        return Result.Success();
    }

    /// <summary>
    /// Marks the portfolio as closed.
    /// Ensures all projects or programs are resolved before transitioning to this status.
    /// </summary>
    public Result Close(LocalDate endDate)
    {
        Guard.Against.Null(endDate, nameof(endDate));

        if (Status is not (ProjectPortfolioStatus.Active or ProjectPortfolioStatus.OnHold))
        {
            return Result.Failure("Only active or on hold portfolios can be closed.");
        }

        if (DateRange == null)
        {
            return Result.Failure("The portfolio must have a start date before it can be closed.");
        }

        if (endDate < DateRange.Start)
        {
            return Result.Failure("The end date cannot be earlier than the start date.");
        }

        if (_programs.Any(p => !p.IsClosed))
        {
            return Result.Failure("All programs must be completed or canceled before the portfolio can be closed.");
        }

        if (_projects.Any(p => !p.IsClosed))
        {
            return Result.Failure("All projects must be completed or canceled before the portfolio can be closed.");
        }

        Status = ProjectPortfolioStatus.Closed;
        DateRange = new FlexibleDateRange(DateRange.Start, endDate);

        return Result.Success();
    }

    /// <summary>
    /// Archives a closed portfolio.
    /// </summary>
    public Result Archive()
    {
        if (Status != ProjectPortfolioStatus.Closed)
        {
            return Result.Failure("Only closed portfolios can be archived.");
        }

        Status = ProjectPortfolioStatus.Archived;

        return Result.Success();
    }

    #endregion Lifecycle

    /// <summary>
    /// Creates and adds a new program to the portfolio.
    /// </summary>
    /// <param name="name">The name of the program.</param>
    /// <param name="description">The description of the program.</param>
    /// <param name="dateRange">The date range of the program (optional).</param>
    /// <param name="roles">The roles associated with the program (optional).</param>
    /// <param name="strategicThemes">The strategic themes associated with the program (optional).</param>
    /// <param name="timestamp"></param>
    /// <returns>A result containing the created program or an error.</returns>
    public Result<Program> CreateProgram(string name, string description, LocalDateRange? dateRange, Dictionary<ProgramRole, HashSet<Guid>>? roles, HashSet<Guid>? strategicThemes, Instant timestamp)
    {
        if (!IsActive)
        {
            return Result.Failure<Program>("Programs can only be created in active or on-hold portfolios.");
        }

        var program = Program.Create(name, description, dateRange, Id, roles, strategicThemes, timestamp);
        _programs.Add(program);

        return Result.Success(program);
    }

    /// <summary>
    /// Creates and adds a new project to the portfolio, optionally associating it with a valid and accepting program.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <param name="description">The description of the project.</param>
    /// <param name="key">The unique code for the project (2-20 uppercase alphanumeric characters or hyphens).</param>
    /// <param name="expenditureCategory">The Id of the expenditure category associated with the project.</param>
    /// <param name="dateRange">The date range of the project.</param>
    /// <param name="programId">The Id of the program the project should be associated with (optional).</param>
    /// <param name="businessCase">The strategic justification for the project (optional).</param>
    /// <param name="expectedBenefits">The measurable outcomes expected from the project (optional).</param>
    /// <param name="roles">The roles associated with the project (optional).</param>
    /// <param name="strategicThemes">The strategic themes associated with the project (optional).</param>
    /// <param name="timestamp"></param>
    /// <returns>A result containing the created project or an error.</returns>
    public Result<Project> CreateProject(string name, string description, ProjectKey key, int expenditureCategory, LocalDateRange? dateRange, Guid? programId, string? businessCase, string? expectedBenefits, Dictionary<ProjectRole, HashSet<Guid>>? roles, HashSet<Guid>? strategicThemes, Instant timestamp)
    {
        if (!IsActive)
        {
            return Result.Failure<Project>("Projects can only be created in active or on-hold portfolios.");
        }

        // Validate the program Id if provided
        Program? program = null;
        if (programId.HasValue)
        {
            program = _programs.SingleOrDefault(p => p.Id == programId.Value);
            if (program is null)
            {
                return Result.Failure<Project>("The specified program does not belong to this portfolio.");
            }

            if (program.AcceptingProjects is false)
            {
                return Result.Failure<Project>("The specified program is not in a valid state to accept projects.");
            }
        }

        // Create the project
        var project = Project.Create(name, description, key, expenditureCategory, dateRange, Id, programId, businessCase, expectedBenefits, roles, strategicThemes, timestamp);

        // Add the project to the portfolio's project list
        _projects.Add(project);

        // Associate the project with the program if provided
        if (program is not null)
        {
            var addToProgramResult = program.AddProject(project);
            if (addToProgramResult.IsFailure)
            {
                return Result.Failure<Project>(addToProgramResult.Error);
            }
        }

        return Result.Success(project);
    }

    public Result ChangeProjectProgram(Guid projectId, Guid? programId)
    {
        var project = _projects.SingleOrDefault(p => p.Id == projectId);
        if (project is null)
        {
            return Result.Failure("The specified project does not belong to this portfolio.");
        }

        if (project.ProgramId == programId)
        {
            return Result.Failure(programId is null
                ? "The project is not currently assigned to a program."
                : "The project is already associated with the specified program.");
        }

        var program = programId.HasValue ? _programs.SingleOrDefault(p => p.Id == programId.Value) : null;
        if (program is null && programId.HasValue)
        {
            return Result.Failure("The specified program does not belong to this portfolio.");
        }

        if (project.ProgramId.HasValue)
        {
            // remove the project from the current program
            var currentProgram = _programs.SingleOrDefault(p => p.Id == project.ProgramId.Value);
            if (currentProgram is null)
            {
                return Result.Failure("The project is associated with an invalid program.");
            }
            var removeProjectResult = currentProgram.RemoveProject(project);
            if (removeProjectResult.IsFailure)
            {
                return Result.Failure(removeProjectResult.Error);
            }
        }

        if (program is not null)
        {
            var addToProgramResult = program.AddProject(project);
            if (addToProgramResult.IsFailure)
            {
                return Result.Failure(addToProgramResult.Error);
            }
        }
        else
        {
            var removeFromProgramResult = project.UpdateProgram(null);
            if (removeFromProgramResult.IsFailure)
            {
                return Result.Failure(removeFromProgramResult.Error);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Deletes the specified program from the portfolio.
    /// </summary>
    /// <param name="programId"></param>
    /// 
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result DeleteProgram(Guid programId, Instant timestamp)
    {
        var program = _programs.SingleOrDefault(p => p.Id == programId);
        if (program is null)
        {
            return Result.Failure("The specified program does not belong to this portfolio.");
        }

        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        if (program.Projects.Count != 0)
        {
            return Result.Failure("The program cannot be deleted while it has associated projects.");
        }

        if (!program.CanBeDeleted())
        {
            return Result.Failure("The program cannot be deleted.");
        }

        _programs.Remove(program);

        AddDomainEvent(new ProgramDeletedEvent(programId, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Deletes the specified project from the portfolio.
    /// </summary>
    /// <param name="projectId"></param>
    /// 
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result DeleteProject(Guid projectId, Instant timestamp)
    {
        var project = _projects.SingleOrDefault(p => p.Id == projectId);
        if (project is null)
        {
            return Result.Failure("The specified project does not belong to this portfolio.");
        }

        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        if (!project.CanBeDeleted())
        {
            return Result.Failure("The project cannot be deleted.");
        }

        if (project.ProgramId.HasValue)
        {
            var program = _programs.SingleOrDefault(p => p.Id == project.ProgramId.Value);
            if (program is null)
            {
                return Result.Failure("The project is associated with an invalid program.");
            }

            var removeProjectResult = program.RemoveProject(project);
            if (removeProjectResult.IsFailure)
            {
                return Result.Failure(removeProjectResult.Error);
            }
        }

        _projects.Remove(project);

        AddDomainEvent(new ProjectDeletedEvent(projectId, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Creates and adds a new strategic initiative to the portfolio.
    /// </summary>
    /// <param name="name">The name of the strategic initiative.</param>
    /// <param name="description">The description of the strategic initiative.</param>
    /// <param name="dateRange">The date range of the strategic initiative.</param>
    /// <param name="roles">The roles associated with the strategic initiative (optional).</param>
    /// <returns>A result containing the created strategic initiative or an error.</returns>
    public Result<StrategicInitiative> CreateStrategicInitiative(string name, string description, LocalDateRange dateRange, Dictionary<StrategicInitiativeRole, HashSet<Guid>>? roles = null)
    {
        if (!IsActive)
        {
            return Result.Failure<StrategicInitiative>("Strategic initiatives can only be created in active or on-hold portfolios.");
        }

        var initiative = StrategicInitiative.Create(name, description, dateRange, Id, roles);
        _strategicInitiatives.Add(initiative);

        return Result.Success(initiative);
    }

    /// <summary>
    /// Deletes the specified strategic initiative from the portfolio.
    /// </summary>
    /// <param name="strategicInitiativeId"></param>
    /// <returns></returns>
    public Result DeleteStrategicInitiative(Guid strategicInitiativeId)
    {
        var strategicInitiative = _strategicInitiatives.SingleOrDefault(p => p.Id == strategicInitiativeId);
        if (strategicInitiative is null)
        {
            return Result.Failure("The specified strategic initiative does not belong to this portfolio.");
        }

        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        if (!strategicInitiative.CanBeDeleted())
        {
            return Result.Failure("The strategic initiative cannot be deleted.");
        }

        _strategicInitiatives.Remove(strategicInitiative);

        return Result.Success();
    }

    /// <summary>
    /// Checks if the portfolio is active on the specified date.
    /// </summary>
    public bool IsActiveOn(LocalDate date)
    {
        Guard.Against.Null(date, nameof(date));

        return DateRange is not null && DateRange.IsActiveOn(date);
    }

    /// <summary>
    /// Creates a new portfolio in the proposed status.
    /// </summary>
    public static ProjectPortfolio Create(string name, string description, Dictionary<ProjectPortfolioRole, HashSet<Guid>>? roles = null)
    {
        return new ProjectPortfolio(name, description, ProjectPortfolioStatus.Proposed, roles);
    }
}
