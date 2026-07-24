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

        // The list rows are built with Mapster (Status/Owner/TaskCount via the DTO's ConfigureMapping).
        var rows = await query
            .ProjectToType<StoryMapListDto>()
            .ToListAsync(cancellationToken);

        // LastModified comes from the SystemLastModified shadow property, which Mapster's projection
        // can't reach, so it is read separately and stitched in by id.
        var lastModifiedById = await query
            .Select(m => new { m.Id, LastModified = EF.Property<Instant>(m, "SystemLastModified") })
            .ToDictionaryAsync(x => x.Id, x => x.LastModified, cancellationToken);

        return rows
            .Select(r => lastModifiedById.TryGetValue(r.Id, out var lastModified)
                ? r with { LastModified = lastModified }
                : r)
            .ToList();
    }
}
