using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>Answers another module asking for one deliverable's current state.</summary>
public sealed class GetSimpleProjectDeliverableQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetSimpleProjectDeliverableQuery, ISimpleProjectDeliverable?>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<ISimpleProjectDeliverable?> Handle(GetSimpleProjectDeliverableQuery request, CancellationToken cancellationToken)
    {
        return await _projectPortfolioManagementDbContext.ProjectDeliverables
            .Where(d => d.Id == request.Id)
            .Select(d => new SimpleProjectDeliverable(
                d.Id, d.ProjectId, d.Name, d.StatusCategory, (ProjectDeliverableStatusAlias)d.StatusAliasValue))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>Answers the Projects Sync job asking for every deliverable's current state.</summary>
public sealed class GetSimpleProjectDeliverablesQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetSimpleProjectDeliverablesQuery, List<ISimpleProjectDeliverable>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ISimpleProjectDeliverable>> Handle(GetSimpleProjectDeliverablesQuery request, CancellationToken cancellationToken)
    {
        var deliverables = await _projectPortfolioManagementDbContext.ProjectDeliverables
            .Select(d => new SimpleProjectDeliverable(
                d.Id, d.ProjectId, d.Name, d.StatusCategory, (ProjectDeliverableStatusAlias)d.StatusAliasValue))
            .ToListAsync(cancellationToken);

        return [.. deliverables.OfType<ISimpleProjectDeliverable>()];
    }
}
