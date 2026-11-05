using Microsoft.Extensions.Options;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;

namespace Wayd.Integrations.AzureDevOps;

/// <summary>
/// Builds organization URLs from <see cref="AzureDevOpsOptions.BaseUrl"/>.
/// </summary>
public sealed class AzureDevOpsUrlResolver(IOptions<AzureDevOpsOptions> options) : IAzureDevOpsUrlResolver
{
    private readonly IOptions<AzureDevOpsOptions> _options = options;

    /// <inheritdoc />
    public string GetOrganizationUrl(string organization)
        => $"{_options.Value.BaseUrl.TrimEnd('/')}/{organization.Trim()}";
}
