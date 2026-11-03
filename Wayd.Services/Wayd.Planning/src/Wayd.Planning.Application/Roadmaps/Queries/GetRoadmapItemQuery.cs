using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Queries;

public sealed record GetRoadmapItemQuery : IQuery<RoadmapItemDetailsDto?>
{
    public GetRoadmapItemQuery(IdOrKey roadmapIdOrKey, Guid itemId)
    {
        IdOrKeyFilter = roadmapIdOrKey.CreateFilter<Roadmap>();
        ItemId = itemId;
    }

    public Expression<Func<Roadmap, bool>> IdOrKeyFilter { get; }
    public Guid ItemId { get; }
}

public sealed class GetRoadmapItemQueryHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal) : IQueryHandler<GetRoadmapItemQuery, RoadmapItemDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<RoadmapItemDetailsDto?> Handle(GetRoadmapItemQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveRoadmapReader(cancellationToken);

        var roadmaps = _planningDbContext.Roadmaps.Where(request.IdOrKeyFilter)
            .Where(reader.Filter);

        var item = await roadmaps
            .SelectMany(r => r.Items)
            .Include(r => r.Parent)
            .Where(r => r.Id == request.ItemId)
            .FirstOrDefaultAsync(cancellationToken);

        if (item == null)
        {
            return null;
        }

        return item switch
        {
            RoadmapActivity activity => activity.Adapt<RoadmapActivityDetailsDto>(),
            RoadmapMilestone milestone => milestone.Adapt<RoadmapMilestoneDetailsDto>(),
            RoadmapTimebox timebox => timebox.Adapt<RoadmapTimeboxDetailsDto>(),
            _ => item.Adapt<RoadmapItemDetailsDto>(),
        };

        //return item.Adapt<RoadmapItemDetailsDto>();  // TODO: this is not working, it's always returning only the BaseRoadmapItem properties
    }
}
