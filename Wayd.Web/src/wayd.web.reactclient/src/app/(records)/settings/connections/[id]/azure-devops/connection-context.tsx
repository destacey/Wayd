import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import { QueryTags } from '@/src/store/features/query-tags'
import {
  QueryActionCreatorResult,
  QueryDefinition,
} from '@reduxjs/toolkit/query'
import { createContext } from 'react'

export interface AzdoConnectionContextInterface {
  connectionId: string
  organizationUrl: string | undefined
  reloadConnectionData: () => QueryActionCreatorResult<
    QueryDefinition<string, any, QueryTags, ConnectionDetailsDto, 'api'>
  >
}

export const AzdoConnectionContext =
  createContext<AzdoConnectionContextInterface | null>(null)
