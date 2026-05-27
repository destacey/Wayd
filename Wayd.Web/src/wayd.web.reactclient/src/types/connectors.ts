export enum ConnectorType {
  AzureDevOps = 0,
  AzureOpenAI = 1,
  OpenAI = 2, // Reserved for future implementation
  Entra = 3,
}

export const CONNECTOR_NAMES: Record<ConnectorType, string> = {
  [ConnectorType.AzureDevOps]: 'Azure DevOps',
  [ConnectorType.AzureOpenAI]: 'Azure OpenAI',
  [ConnectorType.OpenAI]: 'OpenAI',
  [ConnectorType.Entra]: 'Entra',
}

export const CONNECTOR_DESCRIPTIONS: Record<ConnectorType, string> = {
  [ConnectorType.AzureDevOps]:
    'Sync work items, teams, and iterations from Azure DevOps',
  [ConnectorType.AzureOpenAI]:
    'Connect to Azure-hosted OpenAI for AI-powered features',
  [ConnectorType.OpenAI]:
    'Connect to OpenAI API for LLM capabilities (Coming Soon)',
  [ConnectorType.Entra]:
    'Sync people from Microsoft Entra ID via Microsoft Graph',
}
