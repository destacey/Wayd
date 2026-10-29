using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequestTags.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequestTags.Queries;

/// <summary>
/// Intake request tags, on axes and free, sorted by name. Serves both the type-ahead while tagging and the list of
/// free tags an admin manages.
/// </summary>
/// <remarks>
/// Open to every signed-in user, like the axes. A free tag is listed only to a reader who can see a request
/// carrying it, or to the Demand Management Admin: an agent types its name on one desk, and a private desk's
/// labels say something about its requests. Counts read through the caller's read rules.
/// </remarks>
/// <param name="Search">Keeps tags whose name contains this, ignoring case.</param>
/// <param name="IsFree">True for free tags only, false for tags on axes only, null for both.</param>
/// <param name="IncludeInactive">
/// Whether to keep inactive tags and tags on inactive axes, which cannot be applied. Off for a picker.
/// </param>
public sealed record GetIntakeRequestTagsQuery(string? Search = null, bool? IsFree = null, bool IncludeInactive = false)
    : IQuery<List<IntakeRequestTagListItemDto>>;

public sealed class GetIntakeRequestTagsQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestTagsQuery, List<IntakeRequestTagListItemDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<IntakeRequestTagListItemDto>> Handle(GetIntakeRequestTagsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var visibleAssignments = _dbContext.IntakeRequests
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .SelectMany(r => r.Tags);
        var seesEveryFreeTag = reader.IsDemandManagementAdmin;

        var tags = _dbContext.IntakeRequestTags.AsQueryable();
        if (request.IsFree is { } isFree)
            tags = isFree ? tags.Where(t => t.CategoryId == null) : tags.Where(t => t.CategoryId != null);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            tags = tags.Where(t => t.Name.ToLower().Contains(search));
        }

        var rows = await tags
            .Select(t => new
            {
                Tag = t,
                Category = _dbContext.IntakeRequestTagCategories.FirstOrDefault(c => c.Id == t.CategoryId),
                RequestCount = visibleAssignments.Count(a => a.TagId == t.Id),
            })
            .Where(r => r.Tag.CategoryId != null || seesEveryFreeTag || r.RequestCount > 0)
            .Where(r => request.IncludeInactive || (r.Tag.IsActive && (r.Category == null || r.Category.IsActive)))
            .Select(r => new IntakeRequestTagListItemDto
            {
                Id = r.Tag.Id,
                Name = r.Tag.Name,
                Description = r.Tag.Description,
                CategoryId = r.Tag.CategoryId,
                CategoryName = r.Category == null ? null : r.Category.Name,
                IsFree = r.Tag.CategoryId == null,
                IsActive = r.Tag.IsActive,
                RequestCount = r.RequestCount,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.CategoryName, StringComparer.OrdinalIgnoreCase)];
    }
}
