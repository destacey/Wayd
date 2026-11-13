namespace Wayd.Common.Application.Identity.Users;

/// <summary>A user's saved home page arrangement; an empty list means they use the default.</summary>
public sealed record UserHomeLayoutDto
{
    /// <summary>The placed widgets, in the order they show within their columns.</summary>
    public List<UserHomeLayoutWidgetDto> Widgets { get; init; } = [];
}

/// <summary>Where one widget sits on a user's home page, and whether it shows.</summary>
public sealed record UserHomeLayoutWidgetDto
{
    /// <summary>The widget's id in the client's widget registry.</summary>
    public required string WidgetId { get; init; }

    /// <summary>The column the widget sits in.</summary>
    public required HomeLayoutColumn Column { get; init; }

    /// <summary>Whether the widget shows; false when the user turned it off.</summary>
    public required bool Visible { get; init; }

    /// <summary>What the user changed about what the widget shows, by setting key; empty when nothing.</summary>
    public Dictionary<string, string> Settings { get; init; } = [];
}
