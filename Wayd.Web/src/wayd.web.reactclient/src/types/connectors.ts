export enum ConnectorType {
  AzureDevOps = 0,
  AzureOpenAI = 1,
  OpenAI = 2, // Reserved for future implementation
  Entra = 3,
  Workday = 4,
}

export const CONNECTOR_NAMES: Record<ConnectorType, string> = {
  [ConnectorType.AzureDevOps]: 'Azure DevOps',
  [ConnectorType.AzureOpenAI]: 'Azure OpenAI',
  [ConnectorType.OpenAI]: 'OpenAI',
  [ConnectorType.Entra]: 'Entra',
  [ConnectorType.Workday]: 'Workday',
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
  [ConnectorType.Workday]:
    'Sync workers from a Workday tenant via the Staffing SOAP web service',
}

/**
 * Categories of connectors — mirrors `Wayd.Common.Domain.Enums.AppIntegrations.ConnectorCategory`.
 * Used to group the connector picker by purpose and to drive the single-active-per-category rule.
 */
export enum ConnectorCategory {
  Unknown = 0,
  WorkSync = 1,
  PeopleSync = 2,
  AiProvider = 3,
}

/**
 * Display order in the connector picker. Listed in the order an admin most commonly sets things up:
 * who works here → what they're working on → AI features on top.
 */
export const CONNECTOR_CATEGORY_ORDER: ConnectorCategory[] = [
  ConnectorCategory.PeopleSync,
  ConnectorCategory.WorkSync,
  ConnectorCategory.AiProvider,
]

export const CONNECTOR_CATEGORY_LABELS: Record<ConnectorCategory, string> = {
  [ConnectorCategory.Unknown]: 'Other',
  [ConnectorCategory.PeopleSync]: 'People',
  [ConnectorCategory.WorkSync]: 'Work Management',
  [ConnectorCategory.AiProvider]: 'AI Provider',
}

export const CONNECTOR_CATEGORY_DESCRIPTIONS: Record<ConnectorCategory, string> = {
  [ConnectorCategory.Unknown]: '',
  [ConnectorCategory.PeopleSync]:
    'Identify who works at your company. Only one People connector can be active at a time.',
  [ConnectorCategory.WorkSync]:
    'Pull work items, teams, and iterations from your delivery system.',
  [ConnectorCategory.AiProvider]:
    'Power AI features in Wayd. Only one AI Provider can be active at a time.',
}
