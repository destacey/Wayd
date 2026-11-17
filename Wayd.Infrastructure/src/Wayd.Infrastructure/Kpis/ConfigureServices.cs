using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Kpis;
using Wayd.Organization.Application.Teams;
using Wayd.Organization.Domain;

namespace Wayd.Infrastructure.Kpis;

internal static class ConfigureServices
{
    /// <summary>
    /// Registers every kind of record that can carry KPIs: its subject type descriptor and the implementation that
    /// answers who may read and change its KPIs.
    /// </summary>
    /// <remarks>
    /// Descriptors and implementations are registered together, one line each, so a module joining is visible in one
    /// place, as for discussions. Implementations are registered by hand rather than by the marker scan, which binds
    /// one implementation per interface and would collapse them to one. A subject type not registered here is refused
    /// by every KPI endpoint, and its KPI events are filed under the KPI alone.
    /// </remarks>
    internal static IServiceCollection AddKpis(this IServiceCollection services)
    {
        OrganizationKpiSubjects.Register();

        services.AddScoped<IKpiSubjectType, TeamKpiSubjectType>();

        services.AddScoped<KpiSubjectAccessResolver>();

        return services;
    }
}
