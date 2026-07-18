using Wayd.Common.Application.Identity.OidcProviders.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.OidcProviders.Queries;

/// <summary>
/// List all configured OIDC providers — enabled and disabled. Used by the admin
/// Settings UI. Not anonymous; the public providers endpoint goes through
/// <c>/api/auth/providers</c> which only returns enabled rows and a narrower
/// shape.
/// </summary>
public sealed record GetOidcProvidersQuery : IQuery<IReadOnlyList<OidcProviderListItemDto>>;

public sealed class GetOidcProvidersQueryHandler(IWaydDbContext dbContext)
    : IQueryHandler<GetOidcProvidersQuery, IReadOnlyList<OidcProviderListItemDto>>
{
    private readonly IWaydDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<OidcProviderListItemDto>> Handle(
        GetOidcProvidersQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.OidcProviders
            .AsNoTracking()
            .OrderBy(p => p.DisplayName)
            .Select(p => new OidcProviderListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                DisplayName = p.DisplayName,
                ProviderType = p.ProviderType.ToString(),
                Authority = p.Authority,
                IsEnabled = p.IsEnabled,
            })
            .ToListAsync(cancellationToken);
    }
}
