namespace Wayd.Common.Domain.Enums.AppIntegrations;

public static class ConnectorExtensions
{
    /// <summary>
    /// Returns the orchestration category for a connector — drives which background runner
    /// processes it. Add new connectors here when they're introduced.
    /// </summary>
    public static ConnectorCategory GetCategory(this Connector connector) => connector switch
    {
        Connector.AzureDevOps => ConnectorCategory.WorkSync,
        Connector.AzureOpenAI => ConnectorCategory.AiProvider,
        Connector.OpenAI => ConnectorCategory.AiProvider,
        Connector.Entra => ConnectorCategory.PeopleSync,
        Connector.Workday => ConnectorCategory.PeopleSync,
        _ => ConnectorCategory.Unknown
    };
}
