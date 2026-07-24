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
        return await _planningDbContext.StoryMaps
            .Where(request.IdOrKeyFilter)
            .ProjectToType<StoryMapDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
