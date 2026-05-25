'use client'

import {
  ConnectionDetailsDto,
  EntraConnectionDetailsDto,
} from '@/src/services/wayd-api'
import { DetailEntry } from '../_components/detail-registry'
import GenericConnectionDetails from '../_components/generic-connection-details'

const isEntra = (c: ConnectionDetailsDto): c is EntraConnectionDetailsDto =>
  c?.connector?.name === 'Entra'

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  if (!isEntra(connection)) return null
  const config = connection.configuration
  return (
    <GenericConnectionDetails
      connection={connection}
      configFields={[
        { label: 'Tenant ID', value: config?.tenantId },
        { label: 'Client ID', value: config?.clientId },
        { label: 'Client Secret', value: config?.clientSecret, sensitive: true },
        {
          label: 'All Users Group Object ID',
          value: config?.allUsersGroupObjectId,
        },
        {
          label: 'Include Disabled Users',
          value: config?.includeDisabledUsers,
        },
      ]}
    />
  )
}

export const entraDetailEntry: DetailEntry = {
  Details,
}
