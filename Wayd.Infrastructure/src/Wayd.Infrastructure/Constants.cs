namespace Wayd.Infrastructure;

public static class ServiceEndpoints
{
    public const string AlivenessEndpointPath = "/alive";
    public const string HealthEndpointPath = "/api/health";
    public const string StartupEndpointPath = "/startup";
}

public static class AuthConstants
{
    public const string ApiKeyHeaderName = "x-api-key";

    /// <summary>The authentication scheme for a Wayd-issued JWT, which every interactive sign-in ends with.</summary>
    public const string LocalJwtScheme = "LocalJwt";

    /// <summary>The authentication scheme for a personal access token sent in the <see cref="ApiKeyHeaderName"/> header.</summary>
    public const string PersonalAccessTokenScheme = "PersonalAccessToken";
}
