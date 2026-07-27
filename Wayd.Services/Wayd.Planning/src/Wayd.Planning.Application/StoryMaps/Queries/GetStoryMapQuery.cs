using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Queries;

/// <summary>
/// Loads a single story map in full — its goals (with steps and tasks), lanes, and personas.
/// </summary>
public sealed record GetStoryMapQuery : IQuery<StoryMapDetailsDto?>
{
    public GetStoryMapQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<StoryMap>();
    }

    public Expression<Func<StoryMap, bool>> IdOrKeyFilter { get; }
}

public sealed class GetStoryMapQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetStoryMapQuery, StoryMapDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<StoryMapDetailsDto?> Handle(GetStoryMapQuery request, CancellationToken cancellationToken)
    {
        // The DTO graph carries computed members (Status display name, checklist completion counts)
        // and reads JSON-owned collections, so it cannot be produced by a SQL projection. Load the
        // aggregate graph and map it in memory instead.
        var map = await _planningDbContext.StoryMaps
            .Where(request.IdOrKeyFilter)
            .Include(m => m.Owner)
            .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
            .Include(m => m.SwimLanes)
            .Include(m => m.Personas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        return map?.Adapt<StoryMapDetailsDto>();
    }
}
