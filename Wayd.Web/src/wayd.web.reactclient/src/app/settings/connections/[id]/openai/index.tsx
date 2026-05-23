'use client'

import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import { DetailEntry } from '../_components/detail-registry'
import GenericConnectionDetails from '../_components/generic-connection-details'

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  // OpenAI doesn't have its own DTO yet (still reserved in the backend).
  // Render the generic shell — when the backend publishes
  // OpenAIConnectionDetailsDto, swap this to a typed view like azure-openai.
  return <GenericConnectionDetails connection={connection} />
}

export const openAIDetailEntry: DetailEntry = {
  Details,
}
