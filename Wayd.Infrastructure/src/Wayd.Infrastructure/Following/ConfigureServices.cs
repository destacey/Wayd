using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Following;
using Wayd.Common.Application.Records;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Domain;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.Infrastructure.Following;

internal static class ConfigureServices
{
    /// <summary>
    /// Registers every followable owner type with the read rule that reads it, and the store follows are
    /// written through.
    /// </summary>
    /// <remarks>
    /// Owner types and read rules are registered together, one line each, so a module joining is visible in
    /// one place. Read rules are registered by hand rather than by the marker scan, which binds one
    /// implementation per interface and would collapse them to one. <c>FollowRegistrationTests</c> pins
    /// that every owner type registered here has exactly one read rule.
    /// </remarks>
    internal static IServiceCollection AddFollowing(this IServiceCollection services)
    {
        PpmFollowOwners.Register();
        PlanningFollowOwners.Register();
        DemandManagementFollowOwners.Register();

        services.AddScoped<IRecordReadRule, ProjectReadRule>();
        services.AddScoped<IRecordReadRule, ProgramReadRule>();
        services.AddScoped<IRecordReadRule, PlanningIntervalObjectiveReadRule>();
        services.AddScoped<IRecordReadRule, IntakeRequestReadRule>();

        services.AddScoped<IFollowStore, FollowStore>();

        return services;
    }
}
