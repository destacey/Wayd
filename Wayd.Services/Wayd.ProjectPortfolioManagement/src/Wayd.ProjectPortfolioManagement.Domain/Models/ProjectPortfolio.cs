using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a collection of projects or programs that are managed together to achieve strategic results.
/// </summary>
public sealed class ProjectPortfolio : BaseAuditableEntity, IHasIdAndKey
{
    private const string UnauthorizedManageActorError =
        "You are not authorized to manage this portfolio. Portfolio Owners and Managers may.";
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
    /// Read-side authorization predicate: returns true if the given actor may manage this portfolio.
    /// A portfolio has no parent, so only Owner/Manager on the portfolio itself qualifies — or the
    /// domain-wide PPM administrator grant. Sponsors are intentionally excluded — they fund and
    /// oversee but don't run delivery, matching <see cref="Project.CanManageProject(PpmActor, ProjectAncestryRoles)"/>.
    ///
    /// Because a newly created portfolio has no ancestor to inherit leadership from, the administrator
    /// grant is the only way to seed its first Owner.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <returns>True if the actor may manage the portfolio; otherwise, false.</returns>
    public bool CanManagePortfolio(PpmActor actor)
    {
        Guard.Against.Null(actor, nameof(actor));

        return actor.IsPpmAdministrator
            || _roles.Any(r => r.EmployeeId == actor.EmployeeId && r.Role is ProjectPortfolioRole.Owner or ProjectPortfolioRole.Manager);
    }

    /// <summary>
    /// Updates the portfolio details on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="name">The new name.</param>
    /// <param name="description">The new description.</param>
    public Result UpdateDetails(PpmActor actor, string name, string description)
    {
        if (!CanManagePortfolio(actor))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        Name = name;
        Description = description;

        return Result.Success();
    }

    /// <summary>
    /// Assigns an employee to a role on behalf of an actor who must be authorized to manage the portfolio.
    /// Role assignment is gated because it is the path by which membership itself is granted — leaving it
    /// open would let any holder of the Update permission make themselves an Owner.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="role">The role to assign.</param>
    /// <param name="employeeId">The employee receiving the role.</param>
    public Result AssignRole(PpmActor actor, ProjectPortfolioRole role, Guid employeeId)
    {
        if (!CanManagePortfolio(actor))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        return RoleManager.AssignRole(_roles, Id, role, employeeId);
    }

    /// <summary>
    /// Removes an employee from a role on behalf of an actor who must be authorized to manage the portfolio.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="role">The role to remove.</param>
    /// <param name="employeeId">The employee losing the role.</param>
    public Result RemoveRole(PpmActor actor, ProjectPortfolioRole role, Guid employeeId)
    {
        if (!CanManagePortfolio(actor))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        return RoleManager.RemoveAssignment(_roles, role, employeeId);
    }

    /// <summary>
    /// Replaces the portfolio's role assignments on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="updatedRoles">The replacement role assignments.</param>
    public Result UpdateRoles(PpmActor actor, Dictionary<ProjectPortfolioRole, HashSet<Guid>> updatedRoles)
    {
        if (!CanManagePortfolio(actor))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

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

    #region Ranking

    // Whole-number spacing used by a rebalance. Fractional values appear between these as projects
    // are dragged; a rebalance squeezes everything back to clean multiples. The wide step leaves
    // generous float headroom for inserts between rebalances.
    private const double RankStart = 1000d;
    private const double RankStep = 1000d;

    /// <summary>
    /// Whether the actor may rank this portfolio's projects. Identical to
    /// <see cref="CanManagePortfolio"/> — portfolio Owner or Manager, or the domain-wide PPM administrator
    /// grant — because ranking is one of the portfolio's management actions. Surfaced so the API can hint
    /// action availability; enforced inside the ranking methods so no path bypasses it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    public bool CanManageRanking(PpmActor actor) => CanManagePortfolio(actor);

    /// <summary>
    /// Places the ordered <paramref name="orderedProjectIds"/> between two ranked anchors, assigning
    /// each a fractional rank. Either anchor may be null (drop at the top/bottom of the ranking); at
    /// least one must be supplied. Both anchors must already be ranked. Any other portfolio projects
    /// whose rank falls strictly between the anchors — including closed ones the caller can't see —
    /// are folded into the span so they keep distinct slots (no collision). The client owns the
    /// order; this method owns the values.
    /// </summary>
    public Result MoveProjectRanks(
        PpmActor actor,
        IReadOnlyList<Guid> orderedProjectIds,
        Guid? afterProjectId,
        Guid? beforeProjectId)
    {
        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        if (!CanManageRanking(actor))
        {
            return Result.Failure("You are not authorized to rank this portfolio's projects.");
        }

        if (orderedProjectIds is null || orderedProjectIds.Count == 0)
        {
            return Result.Failure("At least one project must be supplied.");
        }

        if (afterProjectId is null && beforeProjectId is null)
        {
            return Result.Failure("At least one anchor must be supplied.");
        }

        if (orderedProjectIds.Distinct().Count() != orderedProjectIds.Count)
        {
            return Result.Failure("The batch contains duplicate projects.");
        }

        foreach (var id in orderedProjectIds)
        {
            if (_projects.All(p => p.Id != id))
            {
                return Result.Failure($"Project {id} does not belong to this portfolio.");
            }
        }

        if (afterProjectId.HasValue && orderedProjectIds.Contains(afterProjectId.Value))
        {
            return Result.Failure("An anchor cannot also be in the batch.");
        }

        if (beforeProjectId.HasValue && orderedProjectIds.Contains(beforeProjectId.Value))
        {
            return Result.Failure("An anchor cannot also be in the batch.");
        }

        Project? after = null;
        if (afterProjectId.HasValue)
        {
            after = _projects.SingleOrDefault(p => p.Id == afterProjectId.Value);
            if (after is null)
            {
                return Result.Failure("The 'after' anchor does not belong to this portfolio.");
            }
        }

        Project? before = null;
        if (beforeProjectId.HasValue)
        {
            before = _projects.SingleOrDefault(p => p.Id == beforeProjectId.Value);
            if (before is null)
            {
                return Result.Failure("The 'before' anchor does not belong to this portfolio.");
            }
        }

        if (after is not null && before is not null && after.Rank >= before.Rank)
        {
            return Result.Failure("The 'after' anchor must rank above the 'before' anchor.");
        }

        // Absent anchor = open end (drop at top/bottom); a present anchor always carries a rank.
        double? lower = after?.Rank;
        double? upper = before?.Rank;

        // Fold in any other projects sitting strictly within the anchor span (including closed ones
        // the client never saw) so they keep distinct slots and nothing collides. They sort after the
        // moved batch within the span.
        var hidden = _projects
            .Where(p => !orderedProjectIds.Contains(p.Id)
                && (lower is null || p.Rank > lower)
                && (upper is null || p.Rank < upper))
            .OrderBy(p => p.Rank)
            .Select(p => p.Id);

        var sequence = orderedProjectIds.Concat(hidden).ToList();

        AssignSpan(sequence, lower, upper);

        return Result.Success();
    }

    /// <summary>
    /// Re-establishes a clean, gap-free, whole-number ranking across the entire portfolio. Ranked
    /// projects (including closed ones) keep their relative order, renumbered from
    /// <see cref="RankStart"/> by <see cref="RankStep"/>; then unranked projects, ordered by name,
    /// continue the sequence. This bootstraps ranking when nothing is ranked yet and is also run
    /// periodically to remove fractional drift and gaps left by closed projects.
    /// </summary>
    /// <param name="actor">
    /// The acting employee and their administrator standing. Pass <see cref="PpmActor.System"/> for
    /// system-initiated maintenance (e.g. a scheduled rebalance), where there is no human actor — the
    /// application layer is responsible for only using it in a trusted system context.
    /// </param>
    public Result RebalanceRanks(PpmActor actor)
    {
        if (IsReadOnly)
        {
            return Result.Failure(ReadOnlyErrorMessage);
        }

        if (!CanManageRanking(actor))
        {
            return Result.Failure("You are not authorized to rank this portfolio's projects.");
        }

        // Every project is ranked, so this simply re-spaces them by their current rank (name as a
        // stable tiebreak for any equal values) back onto clean whole-number multiples of RankStep.
        var ordered = _projects
            .OrderBy(p => p.Rank)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var value = RankStart;
        foreach (var project in ordered)
        {
            project.SetRank(value);
            value += RankStep;
        }

        return Result.Success();
    }

    // Distributes the ordered sequence of project ids into distinct, monotonic ranks.
    //  - Both bounds (between two anchors): evenly divide the gap, strictly between them:
    //      rank_i = lower + (upper - lower) * (i + 1) / (n + 1)
    //  - Top drop (only an upper bound): subdivide toward zero so ranks stay positive:
    //      rank_i = upper * (i + 1) / (n + 1)   (e.g. top 1000 -> 500 -> 250)
    //  - Bottom drop (only a lower bound): step outward by RankStep below the anchor.
    // TODO future: repeated dense inserts into one gap shrink the spacing; a rebalance is the cure.
    private void AssignSpan(IReadOnlyList<Guid> sequence, double? lower, double? upper)
    {
        var count = sequence.Count;

        for (var i = 0; i < count; i++)
        {
            var project = _projects.Single(p => p.Id == sequence[i]);
            double rank;

            if (lower is not null && upper is not null)
            {
                rank = lower.Value + (upper.Value - lower.Value) * (i + 1) / (count + 1);
            }
            else if (upper is not null)
            {
                // Dropped at the top: subdivide toward zero (virtual lower bound of 0) so ranks stay
                // positive and never march into negative space — e.g. top 1000 -> 500 -> 250.
                rank = upper.Value * (i + 1) / (count + 1);
            }
            else
            {
                // Dropped at the bottom: place the batch just below the lower anchor, ascending.
                rank = lower!.Value + (i + 1) * RankStep;
            }

            project.SetRank(rank);
        }
    }

    #endregion Ranking

    #region Lifecycle

    /// <summary>
    /// Activates the portfolio on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="startDate">The date the portfolio becomes active.</param>
    public Result Activate(PpmActor actor, LocalDate startDate)
    {
        if (!CanManagePortfolio(actor))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

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
    /// Closes the portfolio on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="endDate">The date the portfolio closes.</param>
    public Result Close(PpmActor actor, LocalDate endDate)
    {
        if (!CanManagePortfolio(actor))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

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
    /// Archives the portfolio on behalf of an actor who must be authorized to manage it.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    public Result Archive(PpmActor actor)
    {
        if (!CanManagePortfolio(actor))
        {
            return Result.Failure(UnauthorizedManageActorError);
        }

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
    /// <param name="actor">The actor to attribute the project's initial status history row to.</param>
    /// <param name="currentMaxRank">
    /// The highest existing project rank in this portfolio (supplied by the handler via a cheap scalar
    /// query so the aggregate need not load every project). The new project is ranked at the bottom:
    /// <c>currentMaxRank + RankStep</c>, or <see cref="RankStart"/> when this is the first ranked
    /// project. Ranking on create keeps every project ranked, so the board never has null neighbours.
    /// </param>
    /// <returns>A result containing the created project or an error.</returns>
    public Result<Project> CreateProject(string name, string description, ProjectKey key, int expenditureCategory, LocalDateRange? dateRange, Guid? programId, string? businessCase, string? expectedBenefits, Dictionary<ProjectRole, HashSet<Guid>>? roles, HashSet<Guid>? strategicThemes, Instant timestamp, PpmActor actor, double? currentMaxRank = null)
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

        // Rank the project at the bottom of the portfolio's ranking so every project is always ranked
        // from creation. The handler supplies the current max rank to avoid loading all projects.
        var rank = currentMaxRank is null ? RankStart : currentMaxRank.Value + RankStep;

        // Create the project (ranked from construction)
        var project = Project.Create(name, description, key, expenditureCategory, dateRange, Id, rank, programId, businessCase, expectedBenefits, roles, strategicThemes, timestamp, actor);

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

    /// <summary>
    /// Reassigns a project to a different program on behalf of an actor who must be authorized to manage
    /// that project. The rule is the project's, not the portfolio's — reassignment is a change to the
    /// project, so a program-level Owner/Manager qualifies just as they would for any other project edit.
    /// </summary>
    /// <param name="actor">The acting employee and their administrator standing.</param>
    /// <param name="projectId">The project to reassign.</param>
    /// <param name="programId">The new program, or null to remove the project from its program.</param>
    public Result ChangeProjectProgram(PpmActor actor, Guid projectId, Guid? programId)
    {
        var project = _projects.SingleOrDefault(p => p.Id == projectId);
        if (project is null)
        {
            return Result.Failure("The specified project does not belong to this portfolio.");
        }

        // The portfolio owns both collections, so the project's ancestry is assembled here rather than
        // passed in.
        var ancestry = new ProjectAncestryRoles(
            _roles,
            project.ProgramId.HasValue
                ? _programs.SingleOrDefault(p => p.Id == project.ProgramId.Value)?.Roles
                : null);

        if (!project.CanManageProject(actor, ancestry))
        {
            return Result.Failure(
                "You are not authorized to manage this project. Project, program, or portfolio Owners and Managers may.");
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
