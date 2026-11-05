using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Queries;

/// <summary>
/// Lists the story maps the caller can see. By default, archived (Removed) maps are excluded so
/// the list shows current work; pass <see cref="IncludeArchived"/> to include them.
/// </summary>
public sealed record GetStoryMapsQuery(bool IncludeArchived = false) : IQuery<IReadOnlyList<StoryMapListDto>>;

public sealed class GetStoryMapsQueryHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access) : IQueryHandler<GetStoryMapsQuery, IReadOnlyList<StoryMapListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;

    public async Task<IReadOnlyList<StoryMapListDto>> Handle(GetStoryMapsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _access.ResolveReader(cancellationToken);

        var query = _planningDbContext.StoryMaps.Where(reader.Filter);

        if (!request.IncludeArchived)
            query = query.Where(m => m.Status != StatusCategory.Removed);

        // The list rows are built with Mapster (Status/Owners via the DTO's ConfigureMapping).
        var maps = await query
            .OrderByDescending(m => m.Key)
            .ProjectToType<StoryMapListDto>()
            .ToListAsync(cancellationToken);

        if (reader.Actor is not { } actor || maps.Count == 0)
            return maps;

        // Owners can always edit, so the maps the reader can edit include every map they manage.
        var employeeId = actor.EmployeeId;
        var editable = await query
            .Where(StoryMap.EditableBy(employeeId, actor.IsAdministrator, reader.TeamIds))
            .Select(m => new { m.Id, IsOwner = m.Owners.Any(o => o.EmployeeId == employeeId) })
            .ToDictionaryAsync(m => m.Id, m => m.IsOwner, cancellationToken);

        foreach (var map in maps)
        {
            map.CanEdit = editable.TryGetValue(map.Id, out var isOwner);
            map.CanManage = map.CanEdit && (actor.IsAdministrator || isOwner);
        }

        return maps;
    }
}
