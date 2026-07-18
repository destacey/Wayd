using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Identity.OidcProviders.Commands;

namespace Wayd.Infrastructure.Auth.Oidc.Handlers;

/// <summary>
/// Pulls the OIDC discovery document for a configured provider so the admin
/// UI's "Test connection" button can give immediate feedback on whether the
/// Authority is reachable and well-formed. Goes through the same factory the
/// runtime validator uses — a successful test means the JWKS cache is warm
/// and the provider will work for actual token exchanges.
/// </summary>
/// <remarks>
/// Bounded by <see cref="DiscoveryTimeout"/>: a misconfigured Authority could
/// otherwise hang forever (e.g., DNS black-hole). The admin UI is interactive,
/// so a few seconds is the right ceiling — long enough for real network
/// latency, short enough that the operator sees a clear failure instead of
/// staring at a spinner.
/// </remarks>
public sealed class TestOidcProviderDiscoveryCommandHandler(
    IWaydDbContext dbContext,
    IOidcConfigurationManagerFactory configManagerFactory,
    ILogger<TestOidcProviderDiscoveryCommandHandler> logger)
    : ICommandHandler<TestOidcProviderDiscoveryCommand, TestOidcProviderDiscoveryResult>
{
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(10);

    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly IOidcConfigurationManagerFactory _configManagerFactory = configManagerFactory;
    private readonly ILogger<TestOidcProviderDiscoveryCommandHandler> _logger = logger;

    public async Task<Result<TestOidcProviderDiscoveryResult>> Handle(TestOidcProviderDiscoveryCommand request, CancellationToken cancellationToken)
    {
        var provider = await _dbContext.OidcProviders
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (provider is null)
        {
            return Result.Failure<TestOidcProviderDiscoveryResult>("Provider not found.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DiscoveryTimeout);

        try
        {
            var configManager = _configManagerFactory.Get(provider.Authority);
            var config = await configManager.GetConfigurationAsync(timeoutCts.Token);

            _logger.LogInformation(
                "Test discovery succeeded for provider {ProviderId} ({Name}): issuer={Issuer}, keys={KeyCount}.",
                provider.Id, provider.Name, config.Issuer, config.SigningKeys.Count);

            return Result.Success(new TestOidcProviderDiscoveryResult(
                Success: true,
                Issuer: config.Issuer,
                JwksKeyCount: config.SigningKeys.Count,
                Error: null));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Test discovery for provider {ProviderId} ({Name}) timed out after {Seconds}s.",
                provider.Id, provider.Name, DiscoveryTimeout.TotalSeconds);

            return Result.Success(new TestOidcProviderDiscoveryResult(
                Success: false,
                Issuer: null,
                JwksKeyCount: 0,
                Error: $"Discovery request timed out after {DiscoveryTimeout.TotalSeconds:F0}s. Check the Authority URL is reachable."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Test discovery failed for provider {ProviderId} ({Name}).",
                provider.Id, provider.Name);

            return Result.Success(new TestOidcProviderDiscoveryResult(
                Success: false,
                Issuer: null,
                JwksKeyCount: 0,
                Error: ex.Message));
        }
    }
}
