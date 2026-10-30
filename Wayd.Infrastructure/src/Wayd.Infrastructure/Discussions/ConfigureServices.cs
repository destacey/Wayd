using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Discussions;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;

namespace Wayd.Infrastructure.Discussions;

internal static class ConfigureServices
{
    /// <summary>
    /// Registers every kind of record people can discuss: its channel type descriptor and the implementation that
    /// answers who may read and post.
    /// </summary>
    /// <remarks>
    /// Descriptors and implementations are registered together, one line each, so a module joining is visible in one
    /// place, as for following. Implementations are registered by hand rather than by the marker scan, which binds
    /// one implementation per interface and would collapse them to one. A channel type not registered here is
    /// refused by every discussion endpoint.
    /// </remarks>
    internal static IServiceCollection AddDiscussions(this IServiceCollection services)
    {
        DemandManagementDiscussionChannels.Register();

        services.AddScoped<IDiscussionChannelType, IntakeRequestDiscussionChannelType>();

        return services;
    }
}
