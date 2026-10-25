using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Notifications;

namespace Wayd.Infrastructure.Notifications;

/// <summary>Registers notification writing with the container.</summary>
internal static class ConfigureServices
{
    /// <summary>
    /// Registers what writes notifications. The owner types they point at, and the read rules that decide
    /// who may see them, are registered with following.
    /// </summary>
    internal static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<INotificationWriter, NotificationWriter>();

        return services;
    }
}
