using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// How many products in a Proposed or Active status any of the teams is linked to, each product counted once.
/// </summary>
/// <remarks>
/// Counts direct links only, not the children's teams a parent product rolls up when read. The caller supplies the
/// teams, since Product Management holds no memberships: Organization answers which teams an employee is on.
/// </remarks>
/// <param name="TeamIds">The ids of the teams and teams of teams.</param>
public sealed record GetTeamsProductCountQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<int>;

public sealed class GetTeamsProductCountQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetTeamsProductCountQuery, int>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<int> Handle(GetTeamsProductCountQuery query, CancellationToken cancellationToken)
    {
        if (query.TeamIds.Count == 0)
            return 0;

        return await _productManagementDbContext.Products
            .Where(p => p.StatusCategory == StatusCategory.Proposed || p.StatusCategory == StatusCategory.Active)
            .CountAsync(p => p.TeamLinks.Any(l => query.TeamIds.Contains(l.TeamId)), cancellationToken);
    }
}
