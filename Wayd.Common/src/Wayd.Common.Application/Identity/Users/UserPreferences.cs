namespace Wayd.Common.Application.Identity.Users;

/// <summary>
/// Settings a user keeps for themselves, stored as one JSON document on their account. Each part is saved
/// through its own endpoint, so saving one never overwrites another.
/// </summary>
public sealed class UserPreferences
{
    /// <summary>The product tours the user has seen, keyed by tour name.</summary>
    public Dictionary<string, bool> Tours { get; set; } = [];

    /// <summary>The user's theme overrides, or null to use the organization's theme.</summary>
    public UserThemeConfig? ThemeConfig { get; set; }

    /// <summary>The user's own home page arrangement, or null to use the default.</summary>
    public UserHomeLayout? HomeLayout { get; set; }
}

/// <summary>A user's theme overrides.</summary>
public sealed class UserThemeConfig
{
    /// <summary>The primary color as a hex string, or null for the theme's own.</summary>
    public string? ColorPrimary { get; set; }

    /// <summary>Whether the user prefers the compact, denser layout.</summary>
    public bool UseCompactAlgorithm { get; set; }
}

/// <summary>
/// A user's home page arrangement: the widgets they placed, in order. Widget ids belong to the client's
/// widget registry; the server stores them without knowing what they are, so a widget missing from the
/// list shows at its default position and an id the client no longer knows is ignored there.
/// </summary>
public sealed class UserHomeLayout
{
    /// <summary>The placed widgets, in the order they show within their columns.</summary>
    public List<UserHomeLayoutWidget> Widgets { get; set; } = [];

    /// <summary>
    /// The attention tiles the user switched off, by tile id; null when none. Tile ids belong to the client's
    /// widget registry, like widget ids, and the server stores them unread.
    /// </summary>
    public List<string>? MutedTiles { get; set; }
}

/// <summary>Where one widget sits on a user's home page, and whether it shows.</summary>
public sealed class UserHomeLayoutWidget
{
    /// <summary>The widget's id in the client's widget registry.</summary>
    public string WidgetId { get; set; } = default!;

    /// <summary>The column the widget sits in.</summary>
    public HomeLayoutColumn Column { get; set; }

    /// <summary>Whether the widget shows; false when the user turned it off.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// What the user changed about what the widget shows, by setting key; null when nothing. Keys and values
    /// belong to the client's widget registry, like the widget id, and the server stores them unread.
    /// </summary>
    public Dictionary<string, string>? Settings { get; set; }
}

/// <summary>
/// A column of the home page. Saved layouts store the member name, and every user load reads it, so a
/// member is never renamed or removed.
/// </summary>
public enum HomeLayoutColumn
{
    /// <summary>The wide column, two thirds of the page from large screens.</summary>
    Main = 0,

    /// <summary>The narrow column, one third of the page from large screens; below main on a phone.</summary>
    Side = 1,
}
