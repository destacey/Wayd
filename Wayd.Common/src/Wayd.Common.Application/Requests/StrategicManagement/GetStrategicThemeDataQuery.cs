using Wayd.Common.Domain.Interfaces.StrategicManagement;

namespace Wayd.Common.Application.Requests.StrategicManagement;

/// <summary>
/// The current state of one strategic theme, or null when it no longer exists. Read by the PPM module, which
/// keeps a copy of each theme, when an event reaches it for a theme it has no copy of.
/// </summary>
public sealed record GetStrategicThemeDataQuery(Guid Id) : IQuery<IStrategicThemeData?>;
