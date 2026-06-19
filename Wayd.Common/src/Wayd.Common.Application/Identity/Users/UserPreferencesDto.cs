namespace Wayd.Common.Application.Identity.Users;

public sealed record UserPreferencesDto
{
    public Dictionary<string, bool> Tours { get; init; } = [];
    public UserThemeConfigDto? ThemeConfig { get; init; }
}

public sealed record UserThemeConfigDto
{
    public string? ColorPrimary { get; init; }
    public bool UseCompactAlgorithm { get; init; }
}
