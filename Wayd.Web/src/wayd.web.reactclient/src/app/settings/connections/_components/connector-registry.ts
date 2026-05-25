import { ComponentType } from 'react'
import { ConnectorType } from '@/src/types/connectors'
import { ConfigSectionProps } from './azdo-configuration-section'
import { AzureDevOpsConfigurationSection } from './azdo-configuration-section'
import { AzureOpenAIConfigurationSection } from './azure-openai-configuration-section'
import { EntraConfigurationSection } from './entra-configuration-section'

export const CONNECTOR_FORM_REGISTRY: Record<
  ConnectorType,
  ComponentType<ConfigSectionProps>
> = {
  [ConnectorType.AzureDevOps]: AzureDevOpsConfigurationSection,
  [ConnectorType.AzureOpenAI]: AzureOpenAIConfigurationSection,
  [ConnectorType.OpenAI]: AzureOpenAIConfigurationSection, // Placeholder for future implementation
  [ConnectorType.Entra]: EntraConfigurationSection,
}
