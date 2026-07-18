using Wayd.Common.Application.Interfaces.ExternalPeople;

namespace Wayd.AppIntegration.Application.Connections.Commands.Workday;

/// <summary>
/// Lazy-load query for the admin exclusions picker: returns every org of the requested
/// <c>Organization_Type_ID</c> from the tenant tied to <paramref name="ConnectionId"/>. Backed by
/// Workday's <c>Get_Organizations</c> with a server-side type filter — we never page through all
/// orgs to fetch one type, so this is cheap even on tenants with thousands of supervisory orgs.
/// </summary>
// Modeled as ICommand<T> (which returns Result<T>) rather than IQuery<T> because the SOAP call
// genuinely can fail and we want structured error reporting, not exception flow. Class name uses
// the Command suffix to satisfy the architecture-test rule, even though it's conceptually a query
// (no DB writes).
public sealed record GetWorkdayOrgsByTypeCommand(Guid ConnectionId, string OrganizationTypeId) : ICommand<IReadOnlyList<DiscoveredOrg>>;

public sealed class GetWorkdayOrgsByTypeCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IWorkdayConnectionInitializer initializer,
    ILogger<GetWorkdayOrgsByTypeCommandHandler> logger) : ICommandHandler<GetWorkdayOrgsByTypeCommand, IReadOnlyList<DiscoveredOrg>>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IWorkdayConnectionInitializer _initializer = initializer;
    private readonly ILogger<GetWorkdayOrgsByTypeCommandHandler> _logger = logger;

    public async Task<Result<IReadOnlyList<DiscoveredOrg>>> Handle(GetWorkdayOrgsByTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.WorkdayConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);
            if (connection is null)
                return Result.Failure<IReadOnlyList<DiscoveredOrg>>($"Workday connection {request.ConnectionId} not found.");

            var context = new WorkdayRequestContext(
                connection.Configuration.SoapEndpoint,
                connection.Configuration.TenantAlias,
                connection.Configuration.WsdlVersion,
                new WorkdayCredentials(connection.Configuration.IsuUsername, connection.Configuration.IsuPassword),
                connection.Configuration.WorkerKey,
                connection.Configuration.IncludeInactive,
                IncrementalUpdatedFrom: null,
                UseUserIdAsEmailFallback: connection.Configuration.UseUserIdAsEmailFallback,
                UsePreferredName: connection.Configuration.UsePreferredName,
                NormalizeNameCasing: connection.Configuration.NormalizeNameCasing,
                DepartmentOrganizationTypeId: connection.Configuration.DepartmentOrganizationTypeId,
                OrgExclusions: null);

            return await _initializer.GetOrganizationsByType(context, request.OrganizationTypeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", nameof(GetWorkdayOrgsByTypeCommandHandler), request);
            return Result.Failure<IReadOnlyList<DiscoveredOrg>>($"Failed to load orgs: {ex.Message}");
        }
    }
}
