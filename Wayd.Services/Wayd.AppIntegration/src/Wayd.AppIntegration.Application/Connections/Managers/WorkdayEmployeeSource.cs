using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// <see cref="IEmployeeSource"/> adapter for Workday: binds a <see cref="WorkdayConnectionConfiguration"/>
/// and delegates the SOAP fetch to <see cref="IWorkdayEmployeeSource"/>. Registered keyed by
/// <see cref="Connector.Workday"/>. Incremental is supported via Workday's transaction log —
/// the runner's watermark becomes <c>Updated_From</c> on the request.
/// </summary>
public sealed class WorkdayEmployeeSource(IWorkdayEmployeeSource workdayEmployeeSource) : IEmployeeSource
{
    private readonly IWorkdayEmployeeSource _workdayEmployeeSource = workdayEmployeeSource;

    private WorkdayConnectionConfiguration? _cfg;

    public Connector Connector => Connector.Workday;

    public bool SupportsIncremental => true;

    public EmployeeMatchProperty MatchBy => _cfg?.MatchBy ?? EmployeeMatchProperty.Email;

    public Result Bind(SyncableConnectionDescriptor descriptor)
    {
        if (descriptor.Connector != Connector.Workday)
            return Result.Failure($"Descriptor is for connector '{descriptor.Connector}', expected '{Connector.Workday}'.");

        if (descriptor.Configuration is not WorkdayConnectionConfiguration cfg)
            return Result.Failure("Configuration is not WorkdayConnectionConfiguration.");

        _cfg = cfg;
        return Result.Success();
    }

    public async Task<Result<EmployeeFetchResult>> GetEmployees(Instant? since, CancellationToken cancellationToken)
    {
        if (_cfg is null)
            return Result.Failure<EmployeeFetchResult>("Source is not bound to a connection.");

        // Project domain WorkdayOrgExclusion records into the application-layer record. They're
        // the same shape; the layer split exists because Common.Application can't depend on
        // AppIntegration.Domain.
        var exclusions = _cfg.OrgExclusions
            .Select(e => new Common.Application.Interfaces.ExternalPeople.WorkdayOrgExclusion(
                e.OrganizationTypeId, e.OrganizationReference, e.DisplayName))
            .ToList();

        var context = new WorkdayRequestContext(
            _cfg.SoapEndpoint,
            _cfg.TenantAlias,
            _cfg.WsdlVersion,
            new WorkdayCredentials(_cfg.IsuUsername, _cfg.IsuPassword),
            _cfg.WorkerKey,
            _cfg.IncludeInactive,
            IncrementalUpdatedFrom: since,
            UseUserIdAsEmailFallback: _cfg.UseUserIdAsEmailFallback,
            UsePreferredName: _cfg.UsePreferredName,
            NormalizeNameCasing: _cfg.NormalizeNameCasing,
            DepartmentOrganizationTypeId: _cfg.DepartmentOrganizationTypeId,
            OrgExclusions: exclusions);

        var result = await _workdayEmployeeSource.GetEmployees(context, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<EmployeeFetchResult>(result.Error);

        var exclusionCounts = result.Value.ExclusionCounts
            .Select(c => new EmployeeExclusionCount(c.OrganizationTypeId, c.OrganizationReference, c.DisplayName, c.Count))
            .ToList();

        return Result.Success(new EmployeeFetchResult(result.Value.Employees, exclusionCounts));
    }
}
