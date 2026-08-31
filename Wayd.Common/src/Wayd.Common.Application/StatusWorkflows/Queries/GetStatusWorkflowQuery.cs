using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

public sealed record GetStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetStatusWorkflowQuery(IdOrKey idOrKey) => IdOrKeyFilter = idOrKey.CreateFilter<StatusWorkflow>();

    public Expression<Func<StatusWorkflow, bool>> IdOrKeyFilter { get; }
}

public sealed class GetStatusWorkflowQueryHandler(IStatusWorkflowDbContext dbContext)
    : IQueryHandler<GetStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;

    public async Task<StatusWorkflowDetailsDto?> Handle(
        GetStatusWorkflowQuery request, CancellationToken cancellationToken)
    {
        // Statuses are included rather than projected: the can-do flags below are the domain's own
        // rules, and answering "may this publish?" needs the statuses that carry the aliases.
        var workflow = await _dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(request.IdOrKeyFilter, cancellationToken);

        if (workflow is null)
        {
            return null;
        }

        var isAssigned = await _dbContext.WorkflowAssignments
            .AnyAsync(a => a.WorkflowId == workflow.Id, cancellationToken);

        var descriptor = WorkflowOwners.Resolve(workflow.OwnerType);
        var aliasNames = descriptor.IsSuccess ? descriptor.Value.Aliases : new Dictionary<int, string>();

        var missing = workflow.RequiredAliases
            .Where(alias => workflow.StatusFor(alias) is null)
            .Select(alias => aliasNames.TryGetValue(alias, out var name) ? name : alias.ToString())
            .ToList();

        // Mirrors of the aggregate's own guards, so the editor can disable what would be refused rather
        // than let a user find out by trying. The aggregate stays the authority; these only inform.
        var canEdit = !workflow.IsSystem && workflow.State == StatusWorkflowState.Draft;

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
            CanEdit = canEdit,
            CanPublish = canEdit && missing.Count == 0,
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
        };
    }
}
