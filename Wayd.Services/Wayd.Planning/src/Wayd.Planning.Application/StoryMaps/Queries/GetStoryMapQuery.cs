using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Queries;

/// <summary>
/// Loads a single story map in full — its access, goals (with steps and tasks), lanes, and personas — or null when
/// it does not exist or the caller cannot see it.
/// </summary>
public sealed record GetStoryMapQuery : IQuery<StoryMapDetailsDto?>
{
    public GetStoryMapQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<StoryMap>();
    }

    public Expression<Func<StoryMap, bool>> IdOrKeyFilter { get; }
}

public sealed class GetStoryMapQueryHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access) : IQueryHandler<GetStoryMapQuery, StoryMapDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;

    public async Task<StoryMapDetailsDto?> Handle(GetStoryMapQuery request, CancellationToken cancellationToken)
    {
        var reader = await _access.ResolveReader(cancellationToken);

        // The DTO graph carries computed members (Status display name, checklist completion counts)
        // and reads JSON-owned collections, so it cannot be produced by a SQL projection. Load the
        // aggregate graph and map it in memory instead.
        var map = await _planningDbContext.StoryMaps
            .Where(request.IdOrKeyFilter)
            .Where(reader.Filter)
            .Include(m => m.Owners).ThenInclude(o => o.Employee)
            .Include(m => m.Editors).ThenInclude(e => e.Employee)
            .Include(m => m.EditorTeams).ThenInclude(t => t.Team)
            .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
            .Include(m => m.SwimLanes)
            .Include(m => m.Personas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (map is null)
            return null;

        var dto = map.Adapt<StoryMapDetailsDto>();

        if (reader.Actor is { } actor)
        {
            dto.CanManage = map.CanManage(actor);
            dto.CanEdit = map.CanEdit(actor);
        }

        return dto;
    }
}
