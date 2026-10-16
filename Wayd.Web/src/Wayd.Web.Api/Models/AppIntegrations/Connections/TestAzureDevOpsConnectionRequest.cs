namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public class TestAzureDevOpsConnectionRequest
{
    /// <summary>Gets the organization.</summary>
    public string Organization { get; set; } = default!;

    /// <summary>Gets the personal access token.</summary>
    public string PersonalAccessToken { get; set; } = default!;
}
