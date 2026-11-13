namespace Wayd.Web.Api.Models.UserManagement.Profiles;

/// <summary>The signed-in user's saved home page arrangement. No widgets means they use the default.</summary>
public sealed record HomeLayoutResponse
{
    /// <summary>The placed widgets, in the order they show within their columns.</summary>
    public required List<HomeLayoutWidgetResponse> Widgets { get; init; }

    /// <summary>Builds the response from the stored layout.</summary>
    public static HomeLayoutResponse From(UserHomeLayoutDto layout) => new()
    {
        Widgets = [.. layout.Widgets.Select(w => new HomeLayoutWidgetResponse
        {
            WidgetId = w.WidgetId,
            Column = w.Column,
            Visible = w.Visible,
            Settings = w.Settings,
        })],
    };
}

/// <summary>Where one widget sits on the home page, and whether it shows.</summary>
public sealed record HomeLayoutWidgetResponse
{
    /// <summary>The widget's id in the client's widget registry.</summary>
    public required string WidgetId { get; init; }

    /// <summary>The column the widget sits in.</summary>
    public required HomeLayoutColumn Column { get; init; }

    /// <summary>Whether the widget shows; false when the user turned it off.</summary>
    public required bool Visible { get; init; }

    /// <summary>
    /// What the user changed about what the widget shows, by setting key; empty when they changed nothing.
    /// Keys and values are the client's, like the widget id.
    /// </summary>
    public required Dictionary<string, string> Settings { get; init; }
}
