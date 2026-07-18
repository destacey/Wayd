using System.Linq.Expressions;
using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
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

public sealed class GetRoadmapItemQueryHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser) : IQueryHandler<GetRoadmapItemQuery, RoadmapItemDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());

    public async Task<RoadmapItemDetailsDto?> Handle(GetRoadmapItemQuery request, CancellationToken cancellationToken)
    {
        var publicVisibility = Visibility.Public;

        var item = await _planningDbContext.Roadmaps
            .Where(request.IdOrKeyFilter)
            .Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == _currentUserEmployeeId))
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
