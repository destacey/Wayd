namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A connection's editable details taken together, as <see cref="ConnectionDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record ConnectionDetails(string Name, string? Description);

/// <summary>
/// One non-secret configuration value of a connection, named as its connector names it and formatted
/// invariantly. Settings are carried by name because each connector configures different ones; a credential
/// is never a setting.
/// </summary>
public sealed record ConnectionSetting(string Name, string? Value);
