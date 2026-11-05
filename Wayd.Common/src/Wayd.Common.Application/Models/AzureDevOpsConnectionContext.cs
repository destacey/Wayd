namespace Wayd.Common.Application.Models;

/// <summary>
/// The organization and credentials for a single Azure DevOps connection, passed as one unit so an
/// organization and its personal access token can never be crossed between connections. The
/// integration turns the organization name into a URL from the configured base address.
/// </summary>
public sealed record AzureDevOpsConnectionContext(string Organization, string PersonalAccessToken);
