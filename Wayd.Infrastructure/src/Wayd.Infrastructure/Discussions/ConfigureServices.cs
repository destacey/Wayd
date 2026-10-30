using Microsoft.Extensions.DependencyInjection;

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
    /// one implementation per interface and would collapse them to one. Until a module registers here, every
    /// discussion endpoint refuses its channel type.
    /// </remarks>
    internal static IServiceCollection AddDiscussions(this IServiceCollection services)
    {
        return services;
    }
}
