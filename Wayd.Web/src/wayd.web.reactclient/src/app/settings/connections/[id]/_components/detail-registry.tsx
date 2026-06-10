'use client'

import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import { ConnectorType } from '@/src/types/connectors'
import { ItemType } from 'antd/es/menu/interface'
import { ComponentType, ReactNode } from 'react'
import { azureDevOpsDetailEntry } from '../azure-devops'
import { azureOpenAIDetailEntry } from '../azure-openai'
import { entraDetailEntry } from '../entra'
import { workdayDetailEntry } from '../workday'

export interface ConnectionTabDefinition {
  key: string
  label: string
  /**
   * Render the tab body. `connection` is the typed connection DTO returned
   * from the registry's narrowing helper, but at runtime the registry uses
   * `ConnectionDetailsDto` to stay open to future connectors.
   */
  render: (connection: ConnectionDetailsDto) => ReactNode
}

export interface ConnectionActionContext {
  connectionId: string
  connection: ConnectionDetailsDto
  reload: () => void
  canUpdate: boolean
}

export interface DetailEntry {
  /** Component that renders the "Details" tab body. */
  Details: ComponentType<{ connection: ConnectionDetailsDto }>
  /** Optional additional tabs (e.g. AzDO Organization Configuration). */
  extraTabs?: ConnectionTabDefinition[]
  /**
   * Optional component that emits connector-specific menu actions
   * (sync toggle, etc.) by calling `setItems` once per render.
   *
   * Modeled as a component (not a hook) so the page shell can mount it
   * conditionally without violating the Rules of Hooks.
   */
  ExtraActions?: ComponentType<{
    ctx: ConnectionActionContext
    setItems: (items: ItemType[]) => void
  }>
  /**
   * Optional connector-specific wrapper rendered around the page body —
   * used by AzDO to provide its connection context to child components.
   */
  Wrapper?: ComponentType<{
    connection: ConnectionDetailsDto
    reload: () => void
    children: ReactNode
  }>
  /**
   * Optional URL to launch the connection's external system (e.g. AzDO org URL).
   * If provided, an "open in external system" icon renders next to the title.
   */
  getExternalUrl?: (connection: ConnectionDetailsDto) => string | undefined
  /**
   * Override the default edit form for connectors that need custom UI.
   * If omitted, the registry-driven generic edit form is used.
   */
  EditForm?: ComponentType<{
    id: string
    connection: ConnectionDetailsDto
    onFormUpdate: () => void
    onFormCancel: () => void
  }>
}

export const DETAIL_REGISTRY: Partial<Record<ConnectorType, DetailEntry>> = {
  [ConnectorType.AzureDevOps]: azureDevOpsDetailEntry,
  [ConnectorType.AzureOpenAI]: azureOpenAIDetailEntry,
  [ConnectorType.Entra]: entraDetailEntry,
  [ConnectorType.Workday]: workdayDetailEntry,
}

const NAME_TO_TYPE: Record<string, ConnectorType> = {
  'Azure DevOps': ConnectorType.AzureDevOps,
  'Azure OpenAI': ConnectorType.AzureOpenAI,
  'Entra': ConnectorType.Entra,
  'Workday': ConnectorType.Workday,
}

/**
 * Resolve a registry entry from a connection DTO. The backend returns the
 * connector type as `{ id, name }` (SimpleNavigationDto); we map by name so
 * adding a new connector is a single line here plus its registry export.
 */
export const getDetailEntry = (
  connection: ConnectionDetailsDto | undefined,
): DetailEntry | undefined => {
  const name = connection?.connector?.name
  if (!name) return undefined
  const type = NAME_TO_TYPE[name]
  return type !== undefined ? DETAIL_REGISTRY[type] : undefined
}
