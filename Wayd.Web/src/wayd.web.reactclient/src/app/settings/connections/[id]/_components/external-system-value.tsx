'use client'

import { ExternalSystemNavigationDto } from '@/src/services/api/wayd.schemas'

interface ExternalSystemValueProps {
  system: ExternalSystemNavigationDto | null | undefined
}

/**
 * The outside system a connection reaches, linked to its address when it has one. Shows a dash for a connection
 * not tied to a system: one that syncs nothing, or an Azure DevOps connection that has not read its organization's
 * id yet.
 */
const ExternalSystemValue = ({ system }: ExternalSystemValueProps) => {
  if (!system) return <>—</>
  if (!system.baseUrl) return <>{system.name}</>
  return (
    <a href={system.baseUrl} target="_blank" rel="noopener noreferrer">
      {system.name}
    </a>
  )
}

export default ExternalSystemValue
