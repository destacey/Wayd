using System.Data;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

public sealed class StrategicInitiative : BaseAuditableEntity, IHasIdAndKey
{
    private readonly HashSet<RoleAssignment<StrategicInitiativeRole>> _roles = [];
    private readonly HashSet<StrategicInitiativeKpi> _kpis = [];
    private readonly HashSet<StrategicInitiativeProject> _strategicInitiativeProjects = [];

    private StrategicInitiative() { }

    private StrategicInitiative(string name, string description, StrategicInitiativeStatus status, LocalDateRange dateRange, Guid portfolioId, Dictionary<StrategicInitiativeRole, HashSet<Guid>>? roles = null)
    {
        Name = name;
        Description = description;
        Status = status;
        DateRange = dateRange;
        PortfolioId = portfolioId;

        _roles = roles?
            .SelectMany(r => r.Value
                .Select(e => new RoleAssignment<StrategicInitiativeRole>(Id, r.Key, e)))
            .ToHashSet()
            ?? [];
    }

    /// <summary>
    /// The unique key of the strategic initiative.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the strategic initiative.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A detailed explanation of what the strategic initiative aims to achieve.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The status of the strategic initiative.
    /// </summary>
    public StrategicInitiativeStatus Status { get; private set; }

    /// <summary>
    /// The date range for the strategic initiative.
    /// </summary>
    public LocalDateRange DateRange
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(DateRange));
    } = default!;

    /// <summary>
    /// The Id of the portfolio to which this strategic initiative belongs.
    /// </summary>
    public Guid PortfolioId { get; private set; }

    /// <summary>
    /// The portfolio to which this strategic initiative belongs.
    /// </summary>
    public ProjectPortfolio? Portfolio { get; private set; }

    /// <summary>
    /// The roles associated with the strategic initiative.
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<StrategicInitiativeRole>> Roles => _roles;

    /// <summary>
    /// The KPIs associated with this strategic initiative.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiativeKpi> Kpis => _kpis;

    /// <summary>
    /// The projects associated with this strategic initiative.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiativeProject> StrategicInitiativeProjects => _strategicInitiativeProjects;

    /// <summary>
    /// Indicates if the strategic initiative is in a closed state.
    /// </summary>
    public bool IsClosed => Status is StrategicInitiativeStatus.Completed or StrategicInitiativeStatus.Canceled;

    /// Indicates whether the strategic initiative can be deleted.
    /// </summary>
    /// <returns></returns>
    public bool CanBeDeleted() => Status is StrategicInitiativeStatus.Proposed or StrategicInitiativeStatus.Approved;

    /// <summary>
    /// Updates the details of the strategic initiative.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    public Result UpdateDetails(string name, string description, LocalDateRange dateRange)
    {
        Name = name;
        Description = description;
        DateRange = dateRange;

        return Result.Success();
    }

    /// <summary>
    /// Assigns an employee to a specific role within the strategic initiative, allowing multiple employees per role.
    /// </summary>
    public Result AssignRole(StrategicInitiativeRole role, Guid employeeId)
    {
        return RoleManager.AssignRole(_roles, Id, role, employeeId);
    }

    /// <summary>
    /// Removes an employee from a specific role.
    /// </summary>
    public Result RemoveRole(StrategicInitiativeRole role, Guid employeeId)
    {
        return RoleManager.RemoveAssignment(_roles, role, employeeId);
    }

    /// <summary>
    /// Updates the roles for the strategic initiative.
    /// </summary>
    /// <param name="updatedRoles"></param>
    /// <returns></returns>
    public Result UpdateRoles(Dictionary<StrategicInitiativeRole, HashSet<Guid>> updatedRoles)
    {
        return RoleManager.UpdateRoles(_roles, Id, updatedRoles);
    }

    #region Lifecycle

    /// <summary>
    /// Approves the strategic initiative.
    /// </summary>
    public Result Approve()
    {
        if (Status != StrategicInitiativeStatus.Proposed)
        {
            return Result.Failure("Only proposed strategic initiatives can be approved.");
        }

        Status = StrategicInitiativeStatus.Approved;

        return Result.Success();
    }

    /// <summary>
    /// Activates the strategic initiative.
    /// </summary>
    public Result Activate()
    {
        if (Status != StrategicInitiativeStatus.Approved)
        {
            return Result.Failure("Only approved strategic initiatives can be activated.");
        }

        Status = StrategicInitiativeStatus.Active;

        return Result.Success();
    }

    /// <summary>
    /// Marks the strategic initiative as completed.
    /// </summary>
    public Result Complete()
    {
        if (Status is not (StrategicInitiativeStatus.Active or StrategicInitiativeStatus.OnHold))
        {
            return Result.Failure("Only active strategic initiatives can be completed.");
        }

        Status = StrategicInitiativeStatus.Completed;

        return Result.Success();
    }

    /// <summary>
    /// Cancels the strategic initiative.
    /// </summary>
    public Result Cancel()
    {
        if (Status is StrategicInitiativeStatus.Completed or StrategicInitiativeStatus.Canceled)
        {
            return Result.Failure("The strategic initiative is already completed or canceled.");
        }

        Status = StrategicInitiativeStatus.Canceled;

        return Result.Success();
    }

    #endregion Lifecycle

    #region KPIs

    /// <summary>
    /// Creates a new KPI for the strategic initiative.
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public Result<StrategicInitiativeKpi> CreateKpi(StrategicInitiativeKpiUpsertParameters parameters)
    {
        Guard.Against.Null(parameters, nameof(parameters));

        if (IsClosed)
        {
            return Result.Failure<StrategicInitiativeKpi>("KPIs cannot be created for closed strategic initiatives.");
        }

        var nextOrder = _kpis.Count == 0 ? 1 : _kpis.Max(k => k.Order) + 1;

        var kpi = StrategicInitiativeKpi.Create(Id, parameters, nextOrder);

        _kpis.Add(kpi);

        return kpi;
    }

    /// <summary>
    /// Updates an existing KPI for the strategic initiative.
    /// </summary>
    /// <param name="kpiId"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public Result UpdateKpi(Guid kpiId, StrategicInitiativeKpiUpsertParameters parameters)
    {
        Guard.Against.NullOrEmpty(kpiId, nameof(kpiId));
        Guard.Against.Null(parameters, nameof(parameters));

        if (IsClosed)
        {
            return Result.Failure("KPIs cannot be updated for closed strategic initiatives.");
        }

        var kpi = _kpis.FirstOrDefault(k => k.Id == kpiId);
        if (kpi is null)
        {
            return Result.Failure("KPI not found.");
        }

        return kpi.Update(parameters);
    }

    /// <summary>
    /// Deletes a KPI from the strategic initiative.
    /// </summary>
    /// <param name="kpiId"></param>
    /// <returns></returns>
    public Result DeleteKpi(Guid kpiId)
    {
        Guard.Against.NullOrEmpty(kpiId, nameof(kpiId));

        if (IsClosed)
        {
            return Result.Failure("KPIs cannot be deleted for closed strategic initiatives.");
        }

        var kpi = _kpis.FirstOrDefault(k => k.Id == kpiId);
        if (kpi is null)
        {
            return Result.Failure("KPI not found.");
        }

        _kpis.Remove(kpi);

        ResequenceKpiOrder();

        return Result.Success();
    }

    /// <summary>
    /// Reorders the KPIs based on the provided ordered list of KPI IDs.
    /// </summary>
    /// <param name="orderedKpiIds">The KPI IDs in the desired order.</param>
    public Result ReorderKpis(List<Guid> orderedKpiIds)
    {
        Guard.Against.Null(orderedKpiIds, nameof(orderedKpiIds));

        if (IsClosed)
        {
            return Result.Failure("KPIs cannot be reordered for closed strategic initiatives.");
        }

        if (orderedKpiIds.Count != _kpis.Count)
        {
            return Result.Failure("The number of KPI IDs must match the number of existing KPIs.");
        }

        if (orderedKpiIds.Distinct().Count() != orderedKpiIds.Count)
        {
            return Result.Failure("Duplicate KPI IDs are not allowed.");
        }

        for (int i = 0; i < orderedKpiIds.Count; i++)
        {
            var kpi = _kpis.FirstOrDefault(k => k.Id == orderedKpiIds[i]);
            if (kpi is null)
            {
                return Result.Failure($"KPI with ID '{orderedKpiIds[i]}' not found.");
            }

            kpi.Order = i + 1;
        }

        return Result.Success();
    }

    /// <summary>
    /// Resets KPI ordering to eliminate gaps after removal.
    /// </summary>
    private void ResequenceKpiOrder()
    {
        int order = 1;
        foreach (var kpi in _kpis.OrderBy(k => k.Order))
        {
            kpi.Order = order;
            order++;
        }
    }

    #endregion KPIs

    #region Projects

    /// <summary>
    /// Manages the projects associated with the strategic initiative.
    /// </summary>
    /// <param name="projectIds"></param>
    /// <returns></returns>
    public Result ManageProjects(IEnumerable<Guid> projectIds)
    {
        Guard.Against.Null(projectIds, nameof(projectIds));

        if (IsClosed)
        {
            return Result.Failure("Projects cannot be added or removed for closed strategic initiatives.");
        }

        var projectIdSet = projectIds.ToHashSet();
        var existingProjectIds = _strategicInitiativeProjects.Select(p => p.ProjectId).ToHashSet();

        // Remove projects that are no longer in the provided list
        _strategicInitiativeProjects.RemoveWhere(p => !projectIdSet.Contains(p.ProjectId));

        // Add new projects that are not already associated
        var newProjects = projectIdSet.Except(existingProjectIds)
                                      .Select(id => StrategicInitiativeProject.Create(Id, id));
        _strategicInitiativeProjects.UnionWith(newProjects);

        return Result.Success();
    }

    #endregion Projects

    /// <summary>
    /// Creates a new strategic initiative.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="dateRange"></param>
    /// <param name="portfolioId"></param>
    /// <param name="roles"></param>
    /// <returns></returns>
    internal static StrategicInitiative Create(string name, string description, LocalDateRange dateRange, Guid portfolioId, Dictionary<StrategicInitiativeRole, HashSet<Guid>>? roles = null)
    {
        return new StrategicInitiative(name, description, StrategicInitiativeStatus.Proposed, dateRange, portfolioId, roles);
    }
}
