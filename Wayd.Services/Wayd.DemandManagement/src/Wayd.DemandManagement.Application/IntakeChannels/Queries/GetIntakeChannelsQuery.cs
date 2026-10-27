using Wayd.DemandManagement.Application.IntakeChannels.Dtos;

namespace Wayd.DemandManagement.Application.IntakeChannels.Queries;

/// <summary>
/// The intake channels in the order requesters see them: the company's list, or only the channels a desk
/// offers. Archived channels only when asked for, and never as offered by a desk.
/// </summary>
/// <param name="IncludeArchived">Include channels new requests can no longer be given. Ignored with a desk.</param>
/// <param name="DeskId">Only the channels this desk offers; none for a desk that offers none or does not exist.</param>
public sealed record GetIntakeChannelsQuery(bool IncludeArchived = false, Guid? DeskId = null) : IQuery<List<IntakeChannelDto>>;

public sealed class GetIntakeChannelsQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<GetIntakeChannelsQuery, List<IntakeChannelDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public async Task<List<IntakeChannelDto>> Handle(GetIntakeChannelsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.IntakeChannels.AsNoTracking();

        if (request.DeskId is { } deskId)
        {
            var desk = await _dbContext.IntakeDesks
                .Where(d => d.Id == deskId)
                .Select(d => new { d.OffersAllChannels, ChannelIds = d.Channels.Select(c => c.ChannelId).ToList() })
                .FirstOrDefaultAsync(cancellationToken);
            if (desk is null)
                return [];

            query = query.Where(c => c.IsActive);
            if (!desk.OffersAllChannels)
                query = query.Where(c => desk.ChannelIds.Contains(c.Id));
        }
        else if (!request.IncludeArchived)
        {
            query = query.Where(c => c.IsActive);
        }

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
