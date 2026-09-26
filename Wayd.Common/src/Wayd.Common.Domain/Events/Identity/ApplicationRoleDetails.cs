namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A role's editable details taken together, as <see cref="ApplicationRoleDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record ApplicationRoleDetails(string Name, string? Description);
