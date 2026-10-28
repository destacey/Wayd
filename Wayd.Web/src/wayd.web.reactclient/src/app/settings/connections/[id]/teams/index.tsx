'use client'

import {
  ConnectionDetailsDto,
  TeamsConnectionDetailsDto,
} from '@/src/services/wayd-api'
import { DetailEntry } from '../_components/detail-registry'
import GenericConnectionDetails from '../_components/generic-connection-details'

const isTeams = (c: ConnectionDetailsDto) =>
  c?.connector?.name === 'Microsoft Teams'

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  if (!isTeams(connection)) return null
  const config = (connection as TeamsConnectionDetailsDto).configuration
  return (
    <GenericConnectionDetails
      connection={connection}
      configFields={[
        { label: 'Tenant ID', value: config?.tenantId },
        { label: 'Client ID', value: config?.clientId },
        { label: 'Client Secret', value: config?.clientSecret, sensitive: true },
      ]}
    />
  )
}

export const teamsDetailEntry: DetailEntry = {
  Details,
}
