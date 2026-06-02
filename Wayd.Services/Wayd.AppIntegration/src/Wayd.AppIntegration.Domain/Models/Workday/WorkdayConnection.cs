using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models.Workday;

public sealed class WorkdayConnection : Connection<WorkdayConnectionConfiguration>, ISyncableConnection
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private WorkdayConnection() { }
#pragma warning restore CS8618

    private WorkdayConnection(
        string name,
        string? description,
        bool configurationIsValid,
        WorkdayConnectionConfiguration configuration)
    {
        Name = name;
        Description = description;
        IsValidConfiguration = configurationIsValid;
        Connector = Wayd.Common.Domain.Enums.AppIntegrations.Connector.Workday;
        Configuration = Guard.Against.Null(configuration, nameof(Configuration));
    }

    public override WorkdayConnectionConfiguration Configuration { get; protected set; }

    public override bool HasActiveIntegrationObjects => IsValidConfiguration;

    public string? SystemId => null;

    public bool CanSync => IsActive && IsValidConfiguration && HasActiveIntegrationObjects;

    public Result Update(
        string name,
        string? description,
        string wsdlUrl,
        string isuUsername,
        string isuPassword,
        WorkdayWorkerKey workerKey,
        bool includeInactive,
        EmployeeMatchProperty matchBy,
        bool useUserIdAsEmailFallback,
        bool usePreferredName,
        bool normalizeNameCasing,
        string? departmentOrganizationTypeId,
        IReadOnlyList<WorkdayOrgExclusion>? orgExclusions,
        bool configurationIsValid,
        Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
            var newDescription = description?.NullIfWhiteSpacePlusTrim();
            var newWsdlUrl = Guard.Against.NullOrWhiteSpace(wsdlUrl, nameof(wsdlUrl)).Trim();
            var newIsuUsername = Guard.Against.NullOrWhiteSpace(isuUsername, nameof(isuUsername)).Trim();
            var newIsuPassword = Guard.Against.NullOrWhiteSpace(isuPassword, nameof(isuPassword)).Trim();
            var newDepartmentOrgTypeId = string.IsNullOrWhiteSpace(departmentOrganizationTypeId) ? null : departmentOrganizationTypeId.Trim();
            var newOrgExclusions = orgExclusions?.ToList() ?? [];

            if (!UpdateValuesChanged(newName, newDescription, newWsdlUrl, newIsuUsername, newIsuPassword, workerKey, includeInactive, matchBy, useUserIdAsEmailFallback, usePreferredName, normalizeNameCasing, newDepartmentOrgTypeId, newOrgExclusions, configurationIsValid))
                return Result.Success();

            // Parse the URL *before* any mutation so a bad URL doesn't leave the entity
            // half-updated. The command-layer validator already rejects unparseable URLs, so this
            // is defensive — it documents the invariant (no configuration with an unparseable URL
            // ever persists) and protects against any caller that bypasses the validator.
            if (!WorkdayConnectionConfiguration.TryParse(newWsdlUrl, out var parts))
                return Result.Failure(
                    $"WsdlUrl '{newWsdlUrl}' is not a valid Workday Staffing endpoint URL. Expected form: https://{{host}}/ccx/service/{{tenant}}/Staffing/{{version}}.");

            Name = newName;
            Description = newDescription;
            IsValidConfiguration = configurationIsValid;

            Configuration.WsdlUrl = newWsdlUrl;
            Configuration.IsuUsername = newIsuUsername;
            Configuration.IsuPassword = newIsuPassword;
            Configuration.WorkerKey = workerKey;
            Configuration.IncludeInactive = includeInactive;
            Configuration.MatchBy = matchBy;
            Configuration.UseUserIdAsEmailFallback = useUserIdAsEmailFallback;
            Configuration.UsePreferredName = usePreferredName;
            Configuration.NormalizeNameCasing = normalizeNameCasing;
            Configuration.DepartmentOrganizationTypeId = newDepartmentOrgTypeId;
            Configuration.OrgExclusions = newOrgExclusions;

            Configuration.ServiceHost = parts.ServiceHost;
            Configuration.TenantAlias = parts.TenantAlias;
            Configuration.WsdlVersion = parts.WsdlVersion;
            Configuration.SoapEndpoint = parts.SoapEndpoint;

            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// Records the structured result of an init probe (called by Create/Update/Init handlers).
    /// Drives <see cref="Connection.IsValidConfiguration"/>.
    /// </summary>
    public void RecordInitResult(
        bool succeeded,
        IReadOnlyList<string>? missingFields,
        IReadOnlyList<string>? warnings,
        string? authError,
        IReadOnlyList<WorkdayOrgType>? discoveredOrgTypes,
        DateTimeOffset now)
    {
        Guard.Against.Null(Configuration, nameof(Configuration));

        Configuration.LastInitAt = now;
        Configuration.LastInitSucceeded = succeeded;
        Configuration.LastInitMissingFields = missingFields?.ToList();
        Configuration.LastInitWarnings = warnings?.ToList();
        Configuration.LastInitAuthError = authError;

        // Only persist the catalog when the probe actually returned one (null = "probe didn't get
        // far enough"); a successful probe with zero org-types is a real signal we keep.
        if (discoveredOrgTypes is not null)
            Configuration.DiscoveredOrgTypes = [.. discoveredOrgTypes];

        IsValidConfiguration = succeeded;
    }

    private bool UpdateValuesChanged(
        string name,
        string? description,
        string wsdlUrl,
        string isuUsername,
        string isuPassword,
        WorkdayWorkerKey workerKey,
        bool includeInactive,
        EmployeeMatchProperty matchBy,
        bool useUserIdAsEmailFallback,
        bool usePreferredName,
        bool normalizeNameCasing,
        string? departmentOrganizationTypeId,
        IReadOnlyList<WorkdayOrgExclusion> orgExclusions,
        bool configurationIsValid)
    {
        if (!string.Equals(Name, name, StringComparison.Ordinal)) return true;
        if (!string.Equals(Description, description, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.WsdlUrl, wsdlUrl, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.IsuUsername, isuUsername, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.IsuPassword, isuPassword, StringComparison.Ordinal)) return true;
        if (Configuration.WorkerKey != workerKey) return true;
        if (Configuration.IncludeInactive != includeInactive) return true;
        if (Configuration.MatchBy != matchBy) return true;
        if (Configuration.UseUserIdAsEmailFallback != useUserIdAsEmailFallback) return true;
        if (Configuration.UsePreferredName != usePreferredName) return true;
        if (Configuration.NormalizeNameCasing != normalizeNameCasing) return true;
        if (!string.Equals(Configuration.DepartmentOrganizationTypeId, departmentOrganizationTypeId, StringComparison.Ordinal)) return true;
        if (!ExclusionsEqual(Configuration.OrgExclusions, orgExclusions)) return true;
        if (IsValidConfiguration != configurationIsValid) return true;
        return false;
    }

    /// <summary>
    /// Order-insensitive set equality on (TypeId, OrganizationReference) — the DisplayName is a
    /// cosmetic cache, so a change to just the descriptor doesn't constitute a config change.
    /// </summary>
    private static bool ExclusionsEqual(IReadOnlyList<WorkdayOrgExclusion> existing, IReadOnlyList<WorkdayOrgExclusion> incoming)
    {
        if (existing.Count != incoming.Count) return false;
        var existingKeys = new HashSet<(string, string)>(existing.Select(e => (e.OrganizationTypeId, e.OrganizationReference)));
        return incoming.All(i => existingKeys.Contains((i.OrganizationTypeId, i.OrganizationReference)));
    }

    public static WorkdayConnection Create(
        string name,
        string? description,
        WorkdayConnectionConfiguration configuration,
        bool configurationIsValid,
        Instant timestamp)
    {
        var connection = new WorkdayConnection(name, description, configurationIsValid, configuration);

        connection.AddDomainEvent(EntityCreatedEvent.WithEntity(connection, timestamp));

        return connection;
    }
}
