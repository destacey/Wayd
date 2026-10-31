namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Turns an Azure DevOps organization name into its URL, using the configured base address.
/// </summary>
public interface IAzureDevOpsUrlResolver
{
    /// <summary>The organization's URL, such as <c>https://dev.azure.com/contoso</c>.</summary>
    string GetOrganizationUrl(string organization);
}
