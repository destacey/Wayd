using Microsoft.Extensions.Configuration;

namespace Wayd.Integrations.AzureDevOps.IntegrationTests.Models;

public class OptionsFixture : IDisposable
{
    public OptionsFixture()
    {
        var configuration = ConfigurationHelper.GetConfiguration();

        // These are LIVE integration tests: they hit a real Azure DevOps organization using an OrganizationUrl
        // and PersonalAccessToken supplied via an appsettings.json / user-secrets that only exists on a developer
        // machine. In CI (and any environment without that config) the settings are absent, so the tests skip
        // rather than run — see the Assert.Skip guard in the test classes. Build the options only when the
        // required settings are present, so an unconfigured environment does not throw here (which would fail the
        // whole collection before any test can decide to skip).
        var section = configuration.GetSection(AzdoOrganizationOptions.SectionName);
        IsConfigured =
            !string.IsNullOrWhiteSpace(section[nameof(AzdoOrganizationOptions.OrganizationUrl)]) &&
            !string.IsNullOrWhiteSpace(section[nameof(AzdoOrganizationOptions.PersonalAccessToken)]);

        if (IsConfigured)
        {
            AzdoOrganizationOptions = new AzdoOrganizationOptions(configuration);
            ProcessServiceData = new ProcessServiceData(configuration);
        }
    }

    /// <summary>
    /// True when the Azure DevOps organization settings (URL + PAT) are configured, so the live integration
    /// tests can run. False in CI / unconfigured environments, where the tests skip.
    /// </summary>
    public bool IsConfigured { get; }

    public AzdoOrganizationOptions AzdoOrganizationOptions { get; } = null!;
    public ProcessServiceData ProcessServiceData { get; } = null!;

    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
