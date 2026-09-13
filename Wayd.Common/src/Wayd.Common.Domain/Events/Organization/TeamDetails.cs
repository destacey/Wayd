using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A team's editable details taken together, as <see cref="TeamDetailsUpdatedEvent.Previous"/> records the
/// values an edit replaced.
/// </summary>
public sealed record TeamDetails(TeamCode Code, string Name, string? Description);
