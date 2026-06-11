using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// <see cref="IEmployeeSource"/> adapter for Entra: binds an <see cref="EntraConnectionConfiguration"/>
/// and delegates the Graph fetch to <see cref="IEntraEmployeeSource"/>. Registered keyed by
/// <see cref="Connector.Entra"/>. Graph has no change-tracking cursor we use, so incremental is
/// not supported — every run is a full snapshot.
/// </summary>
public sealed class EntraEmployeeSource(IEntraEmployeeSource entraEmployeeSource) : IEmployeeSource
{
    private readonly IEntraEmployeeSource _entraEmployeeSource = entraEmployeeSource;

    private EntraConnectionConfiguration? _cfg;

    public Connector Connector => Connector.Entra;

    public bool SupportsIncremental => false;

    public EmployeeMatchProperty MatchBy => _cfg?.MatchBy ?? EmployeeMatchProperty.Email;

    public Result Bind(SyncableConnectionDescriptor descriptor)
    {
        if (descriptor.Connector != Connector.Entra)
            return Result.Failure($"Descriptor is for connector '{descriptor.Connector}', expected '{Connector.Entra}'.");

        if (descriptor.Configuration is not EntraConnectionConfiguration cfg)
            return Result.Failure("Configuration is not EntraConnectionConfiguration.");

        _cfg = cfg;
        return Result.Success();
    }

    public async Task<Result<EmployeeFetchResult>> GetEmployees(Instant? since, CancellationToken cancellationToken)
    {
        if (_cfg is null)
            return Result.Failure<EmployeeFetchResult>("Source is not bound to a connection.");

        var credentials = new EntraConnectionCredentials(
            _cfg.TenantId,
            _cfg.ClientId,
            _cfg.ClientSecret,
            _cfg.AllUsersGroupObjectId,
            _cfg.IncludeDisabledUsers,
            NormalizeNameCasing: _cfg.NormalizeNameCasing);

        var result = await _entraEmployeeSource.GetEmployees(credentials, cancellationToken);
        return result.IsSuccess
            ? Result.Success(new EmployeeFetchResult([.. result.Value], []))
            : Result.Failure<EmployeeFetchResult>(result.Error);
    }
}
