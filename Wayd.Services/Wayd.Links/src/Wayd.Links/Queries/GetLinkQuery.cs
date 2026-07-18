using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Interfaces;
using Wayd.Links.Models;

namespace Wayd.Links.Queries;

public sealed record GetLinkQuery(Guid LinkId) : IQuery<LinkDto?>;

public sealed class GetLinkQueryHandler : IQueryHandler<GetLinkQuery, LinkDto?>
{
    private readonly ILinksDbContext _linksDbContext;

    public GetLinkQueryHandler(ILinksDbContext linksDbContext)
    {
        _linksDbContext = linksDbContext;
    }

    public async Task<LinkDto?> Handle(GetLinkQuery request, CancellationToken cancellationToken)
    {
        return await _linksDbContext.Links
            .ProjectToType<LinkDto>()
            .FirstOrDefaultAsync(l => l.Id == request.LinkId, cancellationToken);
    }
}
