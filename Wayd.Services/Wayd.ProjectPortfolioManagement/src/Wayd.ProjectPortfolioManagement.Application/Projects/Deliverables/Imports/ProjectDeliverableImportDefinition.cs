using Wayd.Common.Application.Imports;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Imports;

/// <summary>
/// Adds deliverables to existing projects, each row naming its project by key.
/// </summary>
/// <remarks>
/// Runs as <see cref="PpmActor.System"/>: the import is authorized by the caller's
/// Permissions.Projects.Import claim, not by delivery leadership on each project. A finished project's
/// deliverables come across already Accepted or Descoped, since bringing history in is not an edit; an open
/// one on a completed project is refused, because it would leave the project with work outstanding.
/// <para>
/// Per group, keyed on the project, because a project's deliverables are read together — as its order, and
/// as whether it can complete — so a rejected row keeps that project's other deliverables out and leaves every
/// other project alone.
/// </para>
/// </remarks>
public sealed class ProjectDeliverableImportDefinition(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IStatusResolver statusResolver,
    IDateTimeProvider dateTimeProvider,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProjectDeliverableDto>(serializer)
{
    public const string ImportKey = "ppm.project-deliverables";

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public override string Key => ImportKey;
    public override string DisplayName => "Project Deliverables";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Projects;

    public override ImportAtomicity Atomicity => ImportAtomicity.PerGroup;
    public override string? GroupNoun => "project";
    public override int MaxRows => 10_000;

    // Canonical by construction: ProjectKey trims and uppercases, so two rows spelling a key differently
    // still land in the same group.
    protected override string? GroupKey(ImportProjectDeliverableDto row) => row.ProjectKey.Value;

    protected override IReadOnlyList<ImportPass<ImportProjectDeliverableDto>> Steps =>
    [
        new("AddDeliverables", ImportPassScope.Chunked, AddDeliverables),
    ];

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> DescribeCreatedRecords(
        IReadOnlyCollection<Guid> createdEntityIds,
        CancellationToken cancellationToken)
    {
        // A deliverable has no page of its own; the row links to the project it belongs to.
        return await _ppmDbContext.ProjectDeliverables
            .AsNoTracking()
            .Where(d => createdEntityIds.Contains(d.Id))
            .Join(_ppmDbContext.Projects, d => d.ProjectId, p => p.Id, (d, p) => new { d.Id, p.Key, p.Name })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ImportRecordLink(ImportRecordType.Project, x.Key.Value, x.Name),
                cancellationToken);
    }

    private async Task<Result> AddDeliverables(ImportPassContext<ImportProjectDeliverableDto> context, CancellationToken cancellationToken)
    {
        var workflow = await _statusResolver.ForScope(Domain.PpmWorkflowOwners.ProjectDeliverable.Key, scopeId: null, cancellationToken);
        if (workflow.IsFailure)
            return Result.Failure(workflow.Error);

        // Key is persisted through a value converter, so compare against ProjectKey instances: the property
        // translates but a member of it does not. Every deliverable, so new ones are placed after them.
        var keys = context.Rows.Select(r => r.Data.ProjectKey).Distinct().ToList();
        var projectsByKey = (await _ppmDbContext.Projects
                .Include(p => p.Deliverables)
                .Where(p => keys.Contains(p.Key))
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Key.Value, StringComparer.OrdinalIgnoreCase);

        var numbers = context.Rows
            .Select(r => r.Data.OwnerEmployeeNumber?.Trim())
            .OfType<string>()
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var employeesByNumber = (await _ppmDbContext.Employees
                .Where(e => numbers.Contains(e.EmployeeNumber))
                .Select(e => new { e.Id, e.EmployeeNumber })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var key = data.ProjectKey.Value;

            if (!projectsByKey.TryGetValue(key, out var project))
            {
                row.Failed($"No project was found with key '{key}'.");
                continue;
            }

            Guid? ownerId = null;
            if (!string.IsNullOrWhiteSpace(data.OwnerEmployeeNumber))
            {
                var number = data.OwnerEmployeeNumber.Trim();
                if (!employeesByNumber.TryGetValue(number, out var matches))
                {
                    row.Failed($"No employee was found with employee number '{number}'.");
                    continue;
                }

                if (matches.Count > 1)
                {
                    row.Failed($"Employee number '{number}' matches more than one employee.");
                    continue;
                }

                ownerId = matches[0];
            }

            var status = Status(workflow.Value, data.Status);
            if (status.IsFailure)
            {
                row.Failed(status.Error);
                continue;
            }

            var added = project.ImportDeliverable(PpmActor.System, ProjectAncestryRoles.None, data.Name, data.Description,
                data.AcceptanceCriteria, ownerId, data.TargetDate, status.Value, data.StatusReason,
                data.StatusChangedAt ?? _dateTimeProvider.Now);
            if (added.IsFailure)
            {
                row.Failed($"Could not add '{data.Name}' to project '{key}': {added.Error}");
                continue;
            }

            row.Created(added.Value.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// The status a cell names: by its name first, since that is what the person writing the file sees, then by
    /// its meaning, so a file written for the default workflow still lands on a workflow whose stages were renamed.
    /// Blank means Not Started.
    /// </summary>
    private static Result<StatusRef> Status(StatusWorkflow workflow, string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
        {
            return workflow.StatusFor((int)ProjectDeliverableStatusAlias.NotStarted) is { } initial
                ? StatusRef.From(initial)
                : Result.Failure<StatusRef>($"'{workflow.Name}' has no status meaning Not Started.");
        }

        var name = cell.Trim();
        var match = workflow.ActiveStatuses.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? (Meaning(name) is { } alias ? workflow.StatusFor((int)alias) : null);

        return match is null
            ? Result.Failure<StatusRef>($"'{workflow.Name}' has no status named or meaning '{name}'.")
            : StatusRef.From(match);
    }

    /// <summary>
    /// The status meaning a cell names, spaced or not (<c>Not Started</c> or <c>NotStarted</c>).
    /// </summary>
    /// <remarks>
    /// Matched against the names rather than through <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>,
    /// which also reads numbers and combines comma-separated names into an unrelated value.
    /// </remarks>
    private static ProjectDeliverableStatusAlias? Meaning(string name)
    {
        var compact = name.Replace(" ", "", StringComparison.Ordinal);
        foreach (var alias in Enum.GetValues<ProjectDeliverableStatusAlias>())
        {
            if (alias != ProjectDeliverableStatusAlias.None && string.Equals(alias.ToString(), compact, StringComparison.OrdinalIgnoreCase))
                return alias;
        }

        return null;
    }
}
