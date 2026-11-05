namespace Wayd.Common.Application.Models;

/// <summary>
/// App-wide Azure DevOps settings, bound from the <c>AzureDevOps</c> configuration section.
/// </summary>
public sealed class AzureDevOpsOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// The Azure DevOps Services address an organization name is appended to. Configurable so a
    /// change on Microsoft's side needs a setting change rather than a release.
    /// </summary>
    public string BaseUrl { get; set; } = "https://dev.azure.com";
}
