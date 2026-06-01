using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Application.Interfaces.ExternalPeople;

/// <summary>
/// Per-connection credentials used to build a transient Workday SOAP client. The endpoint URL is
/// pre-derived from <c>WsdlUrl</c> at save time so the runtime never reparses.
/// </summary>
public sealed record WorkdayConnectionCredentials(
    string SoapEndpoint,
    string TenantAlias,
    string WsdlVersion,
    string IsuUsername,
    string IsuPassword,
    WorkdayWorkerKey WorkerKey,
    bool IncludeInactive,
    Instant? IncrementalUpdatedFrom,
    bool UseUserIdAsEmailFallback = false,
    bool UsePreferredName = false,
    bool NormalizeNameCasing = true,
    string? DepartmentOrganizationTypeId = null);
