using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows;

/// <inheritdoc cref="IStatusWorkflowDetailsReader"/>
public sealed class StatusWorkflowDetailsReader(IStatusWorkflowDbContext dbContext) : IStatusWorkflowDetailsReader
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;

    public async Task<StatusWorkflowDetailsDto?> Read(
        Expression<Func<StatusWorkflow, bool>> filter,
        Func<TransitionConditionKind, bool> isConditionAvailable,
        CancellationToken cancellationToken)
    {
        // Statuses and transitions are included rather than projected: the can-do flags below are the
        // domain's own rules, and answering "may this publish?" needs the statuses that carry the aliases
        // and the transitions that must reach them.
        var workflow = await _dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
                .ThenInclude(t => t.Conditions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(filter, cancellationToken);

        if (workflow is null)
        {
            return null;
        }

        var isAssigned = await _dbContext.WorkflowAssignments
            .AnyAsync(a => a.WorkflowId == workflow.Id, cancellationToken);

        var descriptor = WorkflowOwners.Resolve(workflow.OwnerType);
        var aliasNames = descriptor.IsSuccess ? descriptor.Value.Aliases : new Dictionary<int, string>();
        var declaresMoves = descriptor.IsSuccess && descriptor.Value.DeclaresMoves;

        var missing = workflow.RequiredAliases
            .Where(alias => workflow.StatusFor(alias) is null)
            .Select(alias => aliasNames.TryGetValue(alias, out var name) ? name : alias.ToString())
            .ToList();

        // Mirrors of the aggregate's own guards, so the editor can disable what would be refused rather
        // than let a user find out by trying. The aggregate stays the authority; these only inform.
        var canEdit = !workflow.IsSystem && workflow.State == StatusWorkflowState.Draft;
        var transitionIssues = workflow.TransitionIssues();
        var missingCategories = workflow.MissingRequiredCategories();
        var misfiledAliases = workflow.MisfiledAliases();
        var statusOrder = workflow.Statuses.ToDictionary(s => s.Id, s => s.Order);

        return new StatusWorkflowDetailsDto
        {
            Id = workflow.Id,
            Key = workflow.Key,
            Name = workflow.Name,
            Description = workflow.Description,
            Owner = GetStatusWorkflowsQueryHandler.DescribeOwner(workflow.OwnerType),
            State = workflow.State.ToString(),
            IsSystem = workflow.IsSystem,
            IsAssigned = isAssigned,
            MissingRequiredAliases = missing,
            MissingRequiredCategories = missingCategories,
            MisfiledAliases = misfiledAliases,
            CanEdit = canEdit,
            CanPublish = canEdit && missing.Count == 0 && missingCategories.Count == 0 && misfiledAliases.Count == 0
                && transitionIssues.Count == 0,
            CanArchive = !workflow.IsSystem && workflow.State == StatusWorkflowState.Published && !isAssigned,
            Statuses =
            [
                .. workflow.Statuses.Select(s => new WorkflowStatusDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Category = SimpleNavigationDto.FromEnum(s.Category),
                    Alias = s.Alias,
                    AliasName = s.Alias != StatusWorkflow.NoAlias && aliasNames.TryGetValue(s.Alias, out var alias)
                        ? alias
                        : null,
                    Order = s.Order,
                }),
            ],
            TransitionMode = workflow.TransitionMode,
            SupportsRestrictedTransitions = workflow.SupportsRestrictedTransitions,
            TransitionIssues = transitionIssues,
            Transitions =
            [
                .. workflow.Transitions
                    .OrderBy(t => statusOrder[t.FromStatusId])
                    .ThenBy(t => statusOrder[t.ToStatusId])
                    .ThenBy(t => t.Id)
                    .Select(t => new WorkflowTransitionDto
                    {
                        Id = t.Id,
                        FromStatusId = t.FromStatusId,
                        ToStatusId = t.ToStatusId,
                        Conditions = t.ConditionKinds(),
                    }),
            ],
            AvailableConditions = declaresMoves ? [] : [.. Enum.GetValues<TransitionConditionKind>().Where(isConditionAvailable)],
            DeclaredMoves =
            [
                .. workflow.DeclaredMoves()
                    .OrderBy(m => statusOrder[m.FromStatusId])
                    .ThenBy(m => statusOrder[m.ToStatusId])
                    .Select(m => new WorkflowMoveDto
                    {
                        FromStatusId = m.FromStatusId,
                        ToStatusId = m.ToStatusId,
                        Actions = [.. m.Moves.Select(move => move.Action)],
                        IsRequired = m.IsRequired,
                        AcceptedConditions = [.. m.AcceptedConditions.Where(isConditionAvailable).Order()],
                    }),
            ],
        };
    }
}
