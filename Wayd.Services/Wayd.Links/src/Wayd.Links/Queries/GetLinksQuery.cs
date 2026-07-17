using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Interfaces;
using Wayd.Links.Models;

namespace Wayd.Links.Queries;

public sealed record GetLinksQuery(Guid ObjectId) : IQuery<IReadOnlyList<LinkDto>>;

public sealed class GetLinksQueryHandler : IQueryHandler<GetLinksQuery, IReadOnlyList<LinkDto>>
{
    private readonly ILinksDbContext _linksDbContext;

    public GetLinksQueryHandler(ILinksDbContext linksDbContext)
    {
        _linksDbContext = linksDbContext;
    }

    public async Task<IReadOnlyList<LinkDto>> Handle(GetLinksQuery request, CancellationToken cancellationToken)
    {
        return await _linksDbContext.Links
            .Where(l => l.ObjectId == request.ObjectId)
            .ProjectToType<LinkDto>()
            .ToListAsync(cancellationToken);
    }
}
