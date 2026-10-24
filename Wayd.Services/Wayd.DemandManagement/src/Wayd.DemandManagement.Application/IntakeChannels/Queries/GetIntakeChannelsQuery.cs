using Wayd.DemandManagement.Application.IntakeChannels.Dtos;

namespace Wayd.DemandManagement.Application.IntakeChannels.Queries;

/// <summary>
/// The intake channels in the order requesters see them, archived ones only when asked for.
/// </summary>
/// <param name="IncludeArchived">Include channels new requests can no longer be given.</param>
public sealed record GetIntakeChannelsQuery(bool IncludeArchived = false) : IQuery<List<IntakeChannelDto>>;

public sealed class GetIntakeChannelsQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<GetIntakeChannelsQuery, List<IntakeChannelDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public async Task<List<IntakeChannelDto>> Handle(GetIntakeChannelsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.IntakeChannels.AsNoTracking();

        if (!request.IncludeArchived)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Order).ThenBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new IntakeChannelDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Order = c.Order,
                IsActive = c.IsActive,
            })
            .ToListAsync(cancellationToken);
    }
}
