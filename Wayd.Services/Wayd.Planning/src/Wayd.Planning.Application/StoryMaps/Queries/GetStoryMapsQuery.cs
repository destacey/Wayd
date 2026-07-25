using Wayd.Common.Domain.Enums.Work;
using Wayd.Planning.Application.StoryMaps.Dtos;

namespace Wayd.Planning.Application.StoryMaps.Queries;

/// <summary>
/// Lists the story maps the user can access. By default, archived (Removed) maps are excluded so
/// the list shows current work; pass <see cref="IncludeArchived"/> to include them.
/// </summary>
public sealed record GetStoryMapsQuery(bool IncludeArchived = false) : IQuery<IReadOnlyList<StoryMapListDto>>;

public sealed class GetStoryMapsQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetStoryMapsQuery, IReadOnlyList<StoryMapListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<StoryMapListDto>> Handle(GetStoryMapsQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.StoryMaps.AsQueryable();

        if (!request.IncludeArchived)
            query = query.Where(m => m.Status != WorkStatusCategory.Removed);

        query = query.OrderByDescending(m => m.Key);

        // The list rows are built with Mapster (Status/Owner via the DTO's ConfigureMapping).
        return await query
            .ProjectToType<StoryMapListDto>()
            .ToListAsync(cancellationToken);
    }
}
