'use client'

import {
  AzureOpenAIConnectionDetailsDto,
  ConnectionDetailsDto,
} from '@/src/services/wayd-api'
import { DetailEntry } from '../_components/detail-registry'
import GenericConnectionDetails from '../_components/generic-connection-details'

const isAzureOpenAI = (
  c: ConnectionDetailsDto,
): c is AzureOpenAIConnectionDetailsDto =>
  c?.connector?.name === 'Azure OpenAI'

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  if (!isAzureOpenAI(connection)) return null
  const config = connection.configuration
  return (
    <GenericConnectionDetails
      connection={connection}
      configFields={[
        { label: 'Base URL', value: config?.baseUrl },
        { label: 'API Key', value: config?.apiKey, sensitive: true },
        { label: 'Deployment Name', value: config?.deploymentName },
        { label: 'Default Temperature', value: config?.defaultTemperature },
        {
          label: 'Default Max Output Tokens',
          value: config?.defaultMaxOutputTokens,
        },
        { label: 'JSON Mode Preferred', value: config?.jsonModePreferred },
      ]}
    />
  )
}

export const azureOpenAIDetailEntry: DetailEntry = {
  Details,
}
