export enum ConnectorType {
  AzureDevOps = 0,
  AzureOpenAI = 1,
  // 2 was OpenAI — removed before any integration shipped; do not reuse the value
  Entra = 3,
  Workday = 4,
}

export const CONNECTOR_NAMES: Record<ConnectorType, string> = {
  [ConnectorType.AzureDevOps]: 'Azure DevOps',
  [ConnectorType.AzureOpenAI]: 'Azure OpenAI',
  [ConnectorType.Entra]: 'Entra',
  [ConnectorType.Workday]: 'Workday',
}

export const CONNECTOR_DESCRIPTIONS: Record<ConnectorType, string> = {
  [ConnectorType.AzureDevOps]:
    'Sync work items, teams, and iterations from Azure DevOps',
  [ConnectorType.AzureOpenAI]:
    'Connect to Azure-hosted OpenAI for AI-powered features',
  [ConnectorType.Entra]:
    'Sync people from Microsoft Entra ID via Microsoft Graph',
  [ConnectorType.Workday]:
    'Sync workers from a Workday tenant via the Staffing SOAP web service',
}

/**
 * Display-category grouping for capabilities. Keys are the capability `category` strings the API
 * sends (the backend `ConnectorCapability` enum's Display GroupName). Listed in the order an
 * admin most commonly sets things up: who works here → what they're working on → AI features on
 * top.
 */
export const CAPABILITY_CATEGORY_ORDER: string[] = [
  'People',
  'Work Management',
  'AI Provider',
]

export const CAPABILITY_CATEGORY_DESCRIPTIONS: Record<string, string> = {
  People:
    'Identify who works at your company. Only one People connector can be active at a time.',
  'Work Management':
    'Pull work items, teams, and iterations from your delivery system.',
  'AI Provider':
    'Power AI features in Wayd. Only one AI Provider can be active at a time.',
}

/** Fallback picker section for capabilities whose category the frontend doesn't recognize. */
export const UNKNOWN_CAPABILITY_CATEGORY = 'Other'

/**
 * Minimal capability shape shared by connector and connection DTOs
 * (structurally compatible with ConnectorCapabilityDto).
 */
interface CapabilityRef {
  id?: number
  name?: string
  category?: string
}

interface CapableDto {
  capabilities?: CapabilityRef[]
}

/** Comma-separated capability names for display (e.g. grid cells, detail panes). */
export const getCapabilityNames = (item: CapableDto | undefined): string =>
  (item?.capabilities ?? [])
    .map((capability) => capability.name)
    .filter(Boolean)
    .join(', ')

/**
 * Distinct display categories across an item's capabilities — the picker groups connectors by
 * these. Falls back to a single "Other" bucket so grouping UIs always have a section.
 */
export const getCapabilityCategories = (item: CapableDto | undefined): string[] => {
  const categories = (item?.capabilities ?? [])
    .map((capability) => capability.category)
    .filter((category): category is string => !!category)

  return categories.length > 0
    ? [...new Set(categories)]
    : [UNKNOWN_CAPABILITY_CATEGORY]
}
