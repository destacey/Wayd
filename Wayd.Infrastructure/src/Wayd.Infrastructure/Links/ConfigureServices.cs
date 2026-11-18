using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Links;
using Wayd.Links;
using Wayd.Organization.Application.Teams;
using Wayd.Organization.Domain;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Application.Risks;
using Wayd.Planning.Domain;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Domain;
using Wayd.ProjectPortfolioManagement.Application.Portfolios;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Domain;

namespace Wayd.Infrastructure.Links;

internal static class ConfigureServices
{
    /// <summary>
    /// Registers every kind of record that can carry links: its owner type descriptor and the implementation that
    /// answers who may read and change its links.
    /// </summary>
    /// <remarks>
    /// Descriptors and implementations are registered together, one line each, so a module joining is visible in one
    /// place, as for KPIs. Implementations are registered by hand rather than by the marker scan, which binds one
    /// implementation per interface and would collapse them to one. An owner type not registered here is refused by
    /// every link endpoint, and its link events are filed under the link alone.
    /// </remarks>
    internal static IServiceCollection AddLinks(this IServiceCollection services)
    {
        OrganizationLinkOwners.Register();
        PlanningLinkOwners.Register();
        PpmLinkOwners.Register();
        ProductManagementLinkOwners.Register();
        WorkLinkOwners.Register();

        services.AddScoped<ILinkOwnerType, TeamLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, PlanningIntervalLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, PlanningIntervalObjectiveLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, RiskLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, PortfolioLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, ProgramLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, ProjectLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, StrategicInitiativeLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, IncidentLinkOwnerType>();
        services.AddScoped<ILinkOwnerType, SprintLinkOwnerType>();

        services.AddScoped<LinkOwnerAccessResolver>();
        services.AddScoped<ILinkWriter, LinkWriter>();

        return services;
    }
}
