namespace Wayd.Common.Application.Identity.Users;

public sealed class UserPreferences
{
    public Dictionary<string, bool> Tours { get; set; } = [];
    public UserThemeConfig? ThemeConfig { get; set; }
}

public sealed class UserThemeConfig
{
    public string? ColorPrimary { get; set; }
    public bool UseCompactAlgorithm { get; set; }
}
