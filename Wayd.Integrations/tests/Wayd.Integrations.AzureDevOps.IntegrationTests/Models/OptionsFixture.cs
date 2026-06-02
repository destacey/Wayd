namespace Wayd.Integrations.AzureDevOps.IntegrationTests.Models;

public class OptionsFixture : IDisposable
{
    public OptionsFixture()
    {
        var configuration = ConfigurationHelper.GetConfiguration();
        AzdoOrganizationOptions = new AzdoOrganizationOptions(configuration);
        ProcessServiceData = new ProcessServiceData(configuration);
    }

    public AzdoOrganizationOptions AzdoOrganizationOptions { get; }
    public ProcessServiceData ProcessServiceData { get; }

    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
