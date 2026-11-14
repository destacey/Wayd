namespace Wayd.Infrastructure.Auth.Local;

public sealed class LocalJwtSettings
{
    public const string SectionName = "SecuritySettings:LocalJwt";

    public string Secret { get; set; } = null!;
    public string Issuer { get; set; } = "https://wayd.dev";
    public string Audience { get; set; } = "https://api.wayd.dev";
    public int TokenExpirationInMinutes { get; set; } = 60;
    public int RefreshTokenExpirationInDays { get; set; } = 7;

    /// <summary>
    /// How long a session lives from sign-in, however often it refreshes: each refresh extends it by
    /// <see cref="RefreshTokenExpirationInDays"/>, never past this. Applies to the web app's sessions and to connected
    /// applications'.
    /// </summary>
    public int SessionLifetimeInDays { get; set; } = 30;
}
