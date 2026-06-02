using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Wayd.Integrations.AzureDevOps.IntegrationTests.Models;

public sealed class AzdoOrganizationOptions : BaseConfiguration
{
    public readonly static string SectionName = "AzdoOrganization";

    [SetsRequiredMembers]
    public AzdoOrganizationOptions(IConfiguration configuration) : base(configuration, SectionName)
    {
        OrganizationUrl = configuration.GetSection(SectionName).GetValue<string>(nameof(OrganizationUrl)) 
            ?? throw new ArgumentNullException(nameof(OrganizationUrl));
        ApiVersion = configuration.GetSection(SectionName).GetValue<string>(nameof(ApiVersion)) 
            ?? throw new ArgumentNullException(nameof(ApiVersion));
        PersonalAccessToken = configuration.GetSection(SectionName).GetValue<string>(nameof(PersonalAccessToken)) 
            ?? throw new ArgumentNullException(nameof(PersonalAccessToken));
    }

    public required string OrganizationUrl { get; init; }
    public required string ApiVersion { get; init; }
    public required string PersonalAccessToken { get; init; }
}
