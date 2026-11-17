import { toInstant } from '@/src/utils/instant'
import {
  ConnectionDetailsDto,
  ConnectionInitResult,
  ConnectionListDto,
  ConnectorListDto,
  CreateConnectionRequest,
  ExternalIdentityMappingDto,
  PagedResponseOfActivityLogDto,
  SyncRunDetailsDto,
  SyncRunListDto,
  SyncType,
  UpdateConnectionRequest,
  UpdateConnectionIdentityMappingRequest,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  connectionsActivateConnection,
  connectionsCreateConnection,
  connectionsDeactivateConnection,
  connectionsDeleteConnection,
  connectionsGetActivities,
  connectionsGetConnection,
  connectionsGetConnectionIdentities,
  connectionsGetConnections,
  connectionsGetConnectors,
  connectionsGetSyncRun,
  connectionsGetSyncRuns,
  connectionsInitConnection,
  connectionsRunSync,
  connectionsTestCommunicationConnection,
  connectionsUpdateConnection,
  connectionsUpdateConnectionIdentity,
  connectionsVerifyTeamsCommunicationConnection,
} from '@/src/services/api/connections'

export interface GetAzdoConnectionTeamsRequest {
  connectionId: string
  workspaceId: string | null
}

export interface GetConnectionIdentitiesRequest {
  connectionId: string
  unmappedOnly?: boolean
}

export const connectionsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getConnections: builder.query<ConnectionListDto[], boolean>({
      queryFn: async (includeDisabled: boolean = false) => {
        try {
          const data = await connectionsGetConnections({
            includeDisabled: includeDisabled,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.Connection,
        ...(result?.map(({ id }) => ({ type: QueryTags.Connection, id })) ??
          []),
      ],
    }),

    getConnection: builder.query<ConnectionDetailsDto, string>({
      queryFn: async (id: string) => {
        try {
          const data = await connectionsGetConnection(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ConnectionDetail, id: arg },
        { type: QueryTags.Connection, id: arg },
      ],
    }),

    getConnectors: builder.query<ConnectorListDto[], void>({
      queryFn: async () => {
        try {
          const data = await connectionsGetConnectors()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Connectors, id: 'LIST' }],
    }),

    createConnection: builder.mutation<string, CreateConnectionRequest>({
      queryFn: async (request) => {
        try {
          const data = await connectionsCreateConnection(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Creating a connector changes whether an incident may offer a Teams chat.
      invalidatesTags: [
        { type: QueryTags.Connection },
        { type: QueryTags.Incident },
      ],
    }),

    updateConnection: builder.mutation<void, UpdateConnectionRequest>({
      queryFn: async (request) => {
        try {
          const data = await connectionsUpdateConnection(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Connection, id: arg.id },
        { type: QueryTags.ConnectionDetail, id: arg.id },
        { type: QueryTags.ActivityLog, id: arg.id },
        { type: QueryTags.Incident },
      ],
    }),

    deleteConnection: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await connectionsDeleteConnection(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.Connection },
        { type: QueryTags.ActivityLog, id },
        { type: QueryTags.Incident },
      ],
    }),

    activateConnection: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await connectionsActivateConnection(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Active state decides whether incidents offer a Teams chat, so refetch them.
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.Connection, id },
        { type: QueryTags.ConnectionDetail, id },
        { type: QueryTags.ActivityLog, id },
        { type: QueryTags.Incident },
      ],
    }),

    deactivateConnection: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await connectionsDeactivateConnection(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.Connection, id },
        { type: QueryTags.ConnectionDetail, id },
        { type: QueryTags.ActivityLog, id },
        { type: QueryTags.Incident },
      ],
    }),

    getSyncRuns: builder.query<
      SyncRunListDto[],
      { connectionId: string; sinceIso?: string }
    >({
      queryFn: async ({ connectionId, sinceIso }) => {
        try {
          const data = await connectionsGetSyncRuns(connectionId, {
            since: sinceIso ? toInstant(new Date(sinceIso)) : undefined,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { connectionId }) => [
        { type: QueryTags.SyncRun, id: connectionId },
      ],
    }),

    runSync: builder.mutation<
      void,
      { connectionId: string; syncType?: SyncType }
    >({
      queryFn: async ({ connectionId, syncType }) => {
        try {
          const data = await connectionsRunSync(connectionId, {
            syncType: syncType,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { connectionId }) => [
        { type: QueryTags.SyncRun, id: connectionId },
      ],
    }),

    initConnection: builder.mutation<ConnectionInitResult, string>({
      queryFn: async (id) => {
        try {
          const data = await connectionsInitConnection(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.Connection, id },
        { type: QueryTags.ConnectionDetail, id },
        { type: QueryTags.ActivityLog, id },
      ],
    }),

    testCommunicationConnection: builder.mutation<void, { id: string }>({
      queryFn: async ({ id }) => {
        try {
          const data = await connectionsTestCommunicationConnection(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    verifyTeamsConnection: builder.mutation<
      void,
      {
        tenantId: string
        clientId: string
        clientSecret: string
      }
    >({
      queryFn: async (request) => {
        try {
          const data =
            await connectionsVerifyTeamsCommunicationConnection(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    getConnectionIdentities: builder.query<
      ExternalIdentityMappingDto[],
      GetConnectionIdentitiesRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await connectionsGetConnectionIdentities(
            request.connectionId,
            { unmappedOnly: request.unmappedOnly },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ConnectionIdentity, id: arg.connectionId },
      ],
    }),

    updateConnectionIdentity: builder.mutation<
      void,
      UpdateConnectionIdentityMappingRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await connectionsUpdateConnectionIdentity(
            request.connectionId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.ConnectionIdentity, id: arg.connectionId },
      ],
    }),

    getConnectionActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await connectionsGetActivities(String(idOrKey), {
            page: page,
            pageSize: pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),

    getSyncRun: builder.query<SyncRunDetailsDto, string>({
      queryFn: async (syncRunId) => {
        try {
          const data = await connectionsGetSyncRun(syncRunId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, syncRunId) => [
        { type: QueryTags.SyncRunDetail, id: syncRunId },
      ],
    }),
  }),
})

export const {
  useGetConnectionsQuery,
  useGetConnectionQuery,
  useGetConnectorsQuery,
  useCreateConnectionMutation,
  useUpdateConnectionMutation,
  useDeleteConnectionMutation,
  useActivateConnectionMutation,
  useDeactivateConnectionMutation,
  useGetSyncRunsQuery,
  useGetSyncRunQuery,
  useRunSyncMutation,
  useInitConnectionMutation,
  useTestCommunicationConnectionMutation,
  useVerifyTeamsConnectionMutation,
  useGetConnectionIdentitiesQuery,
  useUpdateConnectionIdentityMutation,
  useGetConnectionActivitiesQuery,
  useLazyGetConnectionActivitiesQuery,
} = connectionsApi
