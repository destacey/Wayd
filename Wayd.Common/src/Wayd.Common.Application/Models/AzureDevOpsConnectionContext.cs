namespace Wayd.Common.Application.Models;

/// <summary>
/// Endpoint and credentials for a single Azure DevOps connection, passed as one unit so an
/// organization URL and its personal access token can never be crossed between connections.
/// </summary>
public sealed record AzureDevOpsConnectionContext(string OrganizationUrl, string PersonalAccessToken);
