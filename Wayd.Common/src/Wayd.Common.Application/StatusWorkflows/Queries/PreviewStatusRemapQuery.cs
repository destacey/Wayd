using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>
/// What a reassignment would do, before anything is committed.
/// </summary>
/// <remarks>
/// Reassignment rewrites every record of an owner type, so it is built to be reviewed rather than
/// confirmed. This computes the automatic mapping, says how each row was decided, and counts the
/// records behind each one.
/// <para>
/// The remap itself is not persisted between this and the confirmation — it is a value, not a process.
/// The confirming command recomputes the same mapping and applies the operator decisions on top, which
/// is safe because <c>AutoMap</c> is pure over two loaded workflows.
/// </para>
/// </remarks>
public sealed record PreviewStatusRemapQuery(Guid AssignmentId, Guid TargetWorkflowId)
    : IQuery<Result<StatusRemapPreviewDto>>;

public sealed class PreviewStatusRemapQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IStatusRecordCounter> counters)
    : IQueryHandler<PreviewStatusRemapQuery, Result<StatusRemapPreviewDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IStatusRecordCounter> _counters = counters;

    public async Task<Result<StatusRemapPreviewDto>> Handle(
        PreviewStatusRemapQuery request, CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.WorkflowAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        if (assignment is null)
        {
            return Result.Failure<StatusRemapPreviewDto>("Workflow assignment not found.");
        }

        // Both sides need their statuses: AutoMap matches one collection against the other.
        var current = await _dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.Id == assignment.WorkflowId, cancellationToken);

        var target = await _dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.Id == request.TargetWorkflowId, cancellationToken);

        if (current is null || target is null)
        {
            return Result.Failure<StatusRemapPreviewDto>("Status workflow not found.");
        }

        var remap = StatusRemap.AutoMap(current, target);
        if (remap.IsFailure)
        {
            return Result.Failure<StatusRemapPreviewDto>(remap.Error);
        }

        var counter = _counters.FirstOrDefault(c =>
            string.Equals(c.OwnerType, assignment.OwnerType, StringComparison.OrdinalIgnoreCase));

        var counts = counter is null
            ? new Dictionary<Guid, int>()
            : await counter.CountByStatus(current.Id, assignment.ScopeId, cancellationToken);

        var descriptor = WorkflowOwners.Resolve(current.OwnerType);
        var aliasNames = descriptor.IsSuccess ? descriptor.Value.Aliases : new Dictionary<int, string>();

        var targets = target.Statuses.ToList();

        var entries = current.Statuses
            .Select(source =>
            {
                var mapped = remap.Value.For(source.Id);
                var match = targets.FirstOrDefault(t => mapped is not null && t.Id == mapped.StatusId);

                return new StatusRemapEntryDto
                {
                    From = Describe(source, aliasNames),
                    To = match is null ? null : Describe(match, aliasNames),
                    MatchedBy = DescribeMatch(source, match, targets),
                    RecordCount = counts.TryGetValue(source.Id, out var count) ? count : 0,
                };
            })
            .ToList();

        return Result.Success(new StatusRemapPreviewDto
        {
            From = new NavigationDto { Id = current.Id, Key = current.Key, Name = current.Name },
            To = new NavigationDto { Id = target.Id, Key = target.Key, Name = target.Name },
            Entries = entries,
            IsComplete = remap.Value.IsComplete,
            AffectedRecordCount = counts.Values.Sum(),
        });
    }

    /// <summary>
    /// One status, in the shape the rest of the API uses for them.
    /// </summary>
    private static WorkflowStatusDto Describe(WorkflowStatus status, IReadOnlyDictionary<int, string> aliasNames) =>
        new()
        {
            Id = status.Id,
            Name = status.Name,
            Description = status.Description,
            Category = SimpleNavigationDto.FromEnum(status.Category),
            Alias = status.Alias,
            AliasName = status.Alias != StatusWorkflow.NoAlias && aliasNames.TryGetValue(status.Alias, out var name)
                ? name
                : null,
            Order = status.Order,
        };

    /// <summary>
    /// Why a row mapped where it did.
    /// </summary>
    /// <remarks>
    /// The trust signal on the review screen. An alias match is unambiguous and needs no thought; a
    /// name match kept a label the organization chose; a category match is a lone-candidate guess and
    /// is the one worth a second look. Re-derived here rather than returned by the domain, which has no
    /// reason to carry presentation concerns.
    /// </remarks>
    private static string DescribeMatch(
        WorkflowStatus source, WorkflowStatus? match, List<WorkflowStatus> targets)
    {
        if (match is null)
        {
            return "Unresolved";
        }

        if (source.Alias != StatusWorkflow.NoAlias && match.Alias == source.Alias)
        {
            return "Alias";
        }

        if (string.Equals(match.Name, source.Name, StringComparison.OrdinalIgnoreCase))
        {
            return "Name";
        }

        return "Category";
    }
}
