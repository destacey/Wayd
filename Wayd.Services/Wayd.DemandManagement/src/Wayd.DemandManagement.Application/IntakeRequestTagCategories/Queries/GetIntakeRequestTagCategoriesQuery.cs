using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Queries;

/// <summary>
/// The tag axes in presentation order, each with its tags alphabetically.
/// </summary>
/// <remarks>
/// Open to every signed-in user, because whoever can see a request sees its tags and can filter by them. Each
/// tag's request count reads through the caller's read rules, so it never counts a request they cannot see.
/// </remarks>
/// <param name="IncludeInactive">
/// Include axes requests can no longer be tagged along. A settings page manages what a picker hides; an
/// inactive tag on an active axis is always included, because requests may still carry it. Ignored with a desk.
/// </param>
/// <param name="DeskId">
/// Only the active axes this desk offers its agents, which may be none; none for a desk that does not exist.
/// </param>
public sealed record GetIntakeRequestTagCategoriesQuery(bool IncludeInactive = false, Guid? DeskId = null) : IQuery<List<IntakeRequestTagCategoryDto>>;

public sealed class GetIntakeRequestTagCategoriesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestTagCategoriesQuery, List<IntakeRequestTagCategoryDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<IntakeRequestTagCategoryDto>> Handle(GetIntakeRequestTagCategoriesQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var visibleAssignments = _dbContext.IntakeRequests
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .SelectMany(r => r.Tags);

        var query = _dbContext.IntakeRequestTagCategories.AsQueryable();
        if (request.DeskId is { } deskId)
        {
            var desk = await _dbContext.IntakeDesks
                .Where(d => d.Id == deskId)
                .Select(d => new { d.OffersAllTagCategories, CategoryIds = d.TagCategories.Select(c => c.CategoryId).ToList() })
                .FirstOrDefaultAsync(cancellationToken);
            if (desk is null)
                return [];

            query = query.Where(c => c.IsActive);
            if (!desk.OffersAllTagCategories)
                query = query.Where(c => desk.CategoryIds.Contains(c.Id));
        }
        else if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Order).ThenBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new IntakeRequestTagCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                AllowsMany = c.AllowsMany,
                Order = c.Order,
                IsActive = c.IsActive,
                IsSystem = c.IsSystem,
                Tags = c.Tags
                    .OrderBy(t => t.Name).ThenBy(t => t.Id)
                    .Select(t => new IntakeRequestTagOptionDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        IsActive = t.IsActive,
                        RequestCount = visibleAssignments.Count(a => a.TagId == t.Id),
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
    }
}
