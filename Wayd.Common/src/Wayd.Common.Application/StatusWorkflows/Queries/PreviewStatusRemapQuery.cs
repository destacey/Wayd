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
    IEnumerable<IStatusRecordCounter> counters,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<PreviewStatusRemapQuery, Result<StatusRemapPreviewDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IStatusRecordCounter> _counters = counters;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<StatusRemapPreviewDto>> Handle(
        PreviewStatusRemapQuery request, CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.WorkflowAssignments
            .AsNoTracking()
            .Where(a => a.Id == request.AssignmentId)
            .Select(a => new { a.OwnerType, a.ScopeId, WorkflowId = a.Scheme!.DefaultWorkflowId })
            .FirstOrDefaultAsync(cancellationToken);

        // The target must share the assignment's owner type, which the remap checks, so the assignment
        // alone decides whether either side is hidden.
        if (assignment is null || !await _ownerAvailability.IsAvailable(assignment.OwnerType, cancellationToken))
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

        IReadOnlyDictionary<Guid, int> counts = new Dictionary<Guid, int>();
        if (counter is not null)
        {
            var counted = await counter.CountByStatus(current.Id, assignment.ScopeId, cancellationToken);
            if (counted.IsFailure)
            {
                return Result.Failure<StatusRemapPreviewDto>(counted.Error);
            }

            counts = counted.Value;
        }

        return Result.Success(ToPreview(current, target, remap.Value, counts));
    }

    /// <summary>
    /// How each status of <paramref name="current"/> lands in <paramref name="target"/> under the remap, with
    /// the records on each. Both workflows need their statuses loaded.
    /// </summary>
    internal static StatusRemapPreviewDto ToPreview(
        StatusWorkflow current, StatusWorkflow target, StatusRemap remap, IReadOnlyDictionary<Guid, int> counts)
    {
        var descriptor = WorkflowOwners.Resolve(current.OwnerType);
        var owner = descriptor.IsSuccess ? descriptor.Value : null;

        var targets = target.Statuses.ToList();

        var entries = current.Statuses
            .Select(source =>
            {
                var mapped = remap.For(source.Id);
                var match = targets.FirstOrDefault(t => mapped is not null && t.Id == mapped.StatusId);

                return new StatusRemapEntryDto
                {
                    From = Describe(source, owner),
                    To = match is null ? null : Describe(match, owner),
                    MatchedBy = DescribeMatch(source, match, targets),
                    RecordCount = counts.TryGetValue(source.Id, out var count) ? count : 0,
                };
            })
            .ToList();

        return new StatusRemapPreviewDto
        {
            From = new NavigationDto { Id = current.Id, Key = current.Key, Name = current.Name },
            To = new NavigationDto { Id = target.Id, Key = target.Key, Name = target.Name },
            Entries = entries,
            IsComplete = remap.IsComplete,
            AffectedRecordCount = counts.Values.Sum(),
        };
    }

    /// <summary>
    /// One status, in the shape the rest of the API uses for them.
    /// </summary>
    private static WorkflowStatusDto Describe(WorkflowStatus status, WorkflowOwnerDescriptor? owner) =>
        new()
        {
            Id = status.Id,
            Name = status.Name,
            Description = status.Description,
            Category = StatusCategoryNavigation.For(status.Category, owner),
            Alias = status.Alias,
            AliasName = status.Alias != StatusWorkflow.NoAlias && owner is not null && owner.Aliases.TryGetValue(status.Alias, out var name)
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
