using System.Reflection;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// The intake request event types a reader may see in a request's history, by the tiers they hold.
/// </summary>
/// <remarks>
/// Read from each event's declared <see cref="IIntakeRequestEvent.ReadTier"/>, once, so an event added later
/// is filtered by the tier it declares without anyone remembering to list it here.
/// </remarks>
internal static class IntakeRequestEventTiers
{
    private static readonly IReadOnlyList<(string EventType, IntakeRequestReadTier Tier)> Events =
        [.. typeof(IIntakeRequestEvent).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIntakeRequestEvent).IsAssignableFrom(t))
            .Select(t => (t.Name, (IntakeRequestReadTier)t
                .GetProperty(nameof(IIntakeRequestEvent.ReadTier), BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!))];

    /// <summary>
    /// The event type names in the given tiers.
    /// </summary>
    public static IReadOnlyCollection<string> EventTypesIn(IReadOnlyCollection<IntakeRequestReadTier> tiers) =>
        [.. Events.Where(e => tiers.Contains(e.Tier)).Select(e => e.EventType)];
}
