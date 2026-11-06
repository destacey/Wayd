namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Microsoft Graph app-registration credentials for a Teams communication connection.
/// Each call builds its own <c>GraphServiceClient</c> from these values.
/// </summary>
public sealed record TeamsGraphCredentials(
    string TenantId,
    string ClientId,
    string ClientSecret);
