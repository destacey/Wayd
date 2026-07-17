using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.OidcProviders.Commands;

namespace Wayd.Infrastructure.Auth.Oidc.Handlers;

/// <summary>
/// Lives in Infrastructure because it needs <see cref="IUserIdentityStore"/> for
/// the active-identity reference check. The store is internal to Infrastructure,
/// and the application layer shouldn't need to know about <c>UserIdentity</c>
/// internals just to support OIDC provider deletion.
/// </summary>
public sealed class DeleteOidcProviderCommandHandler(
    IWaydDbContext dbContext,
    IUserIdentityStore userIdentityStore,
    IOidcProviderRegistry registry,
    ILogger<DeleteOidcProviderCommandHandler> logger)
    : ICommandHandler<DeleteOidcProviderCommand, DeleteOidcProviderResult>
{
    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly IUserIdentityStore _userIdentityStore = userIdentityStore;
    private readonly IOidcProviderRegistry _registry = registry;
    private readonly ILogger<DeleteOidcProviderCommandHandler> _logger = logger;

    public async Task<Result<DeleteOidcProviderResult>> Handle(DeleteOidcProviderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _dbContext.OidcProviders
                .SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (provider is null)
            {
                return Result.Failure<DeleteOidcProviderResult>("Provider not found.");
            }

            // Refuse delete while any user still has an active identity bound to
            // this provider. The frontend renders the count so the admin can go
            // unlink / rebind those users before retrying.
            var activeCount = await _userIdentityStore.CountActiveByProvider(provider.Name, cancellationToken);
            if (activeCount > 0)
            {
                _logger.LogInformation(
                    "Refusing to delete provider {ProviderId} ({Name}): {Count} active user identities still reference it.",
                    provider.Id, provider.Name, activeCount);

                return Result.Success(new DeleteOidcProviderResult(Deleted: false, ActiveIdentityCount: activeCount));
            }

            _dbContext.OidcProviders.Remove(provider);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _registry.Invalidate(provider.Name);

            _logger.LogInformation("Deleted OIDC provider {ProviderId} ({Name}).", provider.Id, provider.Name);
            return Result.Success(new DeleteOidcProviderResult(Deleted: true, ActiveIdentityCount: 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete OIDC provider {ProviderId}.", request.Id);
            return Result.Failure<DeleteOidcProviderResult>("Failed to delete OIDC provider.");
        }
    }
}
