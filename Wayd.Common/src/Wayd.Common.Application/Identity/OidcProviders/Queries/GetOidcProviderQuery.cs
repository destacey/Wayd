using MediatR;
using Wayd.Common.Application.Identity.OidcProviders.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.OidcProviders.Queries;

/// <summary>
/// Returns full provider detail by Id. Used by the admin UI's edit form.
/// Returns null if not found — the controller surfaces that as 404.
/// </summary>
public sealed record GetOidcProviderQuery(Guid Id) : IQuery<OidcProviderDto?>;

internal sealed class GetOidcProviderQueryHandler(IWaydDbContext dbContext)
    : IRequestHandler<GetOidcProviderQuery, OidcProviderDto?>
{
    private readonly IWaydDbContext _dbContext = dbContext;

    public async Task<OidcProviderDto?> Handle(
        GetOidcProviderQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.OidcProviders
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new OidcProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                DisplayName = p.DisplayName,
                ProviderType = p.ProviderType.ToString(),
                Authority = p.Authority,
                ClientId = p.ClientId,
                Audience = p.Audience,
                Scopes = p.Scopes,
                AllowedTenantIds = p.AllowedTenantIds,
                ClockSkewSeconds = p.ClockSkewSeconds,
                IsEnabled = p.IsEnabled,
                AllowAutoRegistration = p.RegistrationPolicy.AllowAutoRegistration,
                // Dependent settings are NULL on a disabled policy; surface the safe
                // default (employee gate on) so the form shows it when re-enabling.
                RequireEmployeeRecord = p.RegistrationPolicy.RequireEmployeeRecord ?? true,
                DefaultRoleId = p.RegistrationPolicy.DefaultRoleId,
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
