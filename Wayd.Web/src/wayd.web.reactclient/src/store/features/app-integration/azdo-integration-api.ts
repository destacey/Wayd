import {
  AzdoConnectionTeamMappingsRequest,
  AzureDevOpsWorkspaceTeamDto,
  InitWorkspaceIntegrationRequest,
  TestAzureDevOpsConnectionRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { getAzureDevOpsConnectionsClient } from '@/src/services/clients'
import { QueryTags } from '../query-tags'

export interface GetAzdoConnectionTeamsRequest {
  connectionId: string
  workspaceId: string | null
}

export interface SetAzdoWorkspaceSyncRequest {
  connectionId: string
  workspaceExternalId: string
  isEnabled: boolean
}

export const azdoIntegrationApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    syncAzdoConnectionOrganization: builder.mutation<void, string>({
      queryFn: async (connectionId) => {
        try {
          const data =
            await getAzureDevOpsConnectionsClient().syncOrganizationConfiguration(
              connectionId,
            )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.AzdoConnectionDetail, id: arg },
          { type: QueryTags.ConnectionDetail, id: arg },
          { type: QueryTags.Connection, id: arg },
        ]
      },
    }),

    setAzdoWorkspaceSync: builder.mutation<void, SetAzdoWorkspaceSyncRequest>({
      queryFn: async (request) => {
        try {
          const data = await getAzureDevOpsConnectionsClient().setWorkspaceSync(
            request.connectionId,
            request.workspaceExternalId,
            { isEnabled: request.isEnabled },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.AzdoConnectionDetail, id: arg.connectionId },
          { type: QueryTags.ConnectionDetail, id: arg.connectionId },
          { type: QueryTags.Connection, id: arg.connectionId },
        ]
      },
    }),

    initAzdoConnectionWorkspace: builder.mutation<
      void,
      InitWorkspaceIntegrationRequest
    >({
      queryFn: async (request) => {
        try {
          const data =
            await getAzureDevOpsConnectionsClient().initWorkspaceIntegration(
              request.id,
              request,
            )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.AzdoConnectionDetail, id: arg.id },
          { type: QueryTags.ConnectionDetail, id: arg.id },
          { type: QueryTags.Connection, id: arg.id },
        ]
      },
    }),

    getAzdoConnectionTeams: builder.query<
      AzureDevOpsWorkspaceTeamDto[],
      GetAzdoConnectionTeamsRequest
    >({
      queryFn: async (request: GetAzdoConnectionTeamsRequest) => {
        try {
          const data =
            await getAzureDevOpsConnectionsClient().getConnectionTeams(
              request.connectionId,
              request.workspaceId,
            )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.AzdoConnectionTeam,
        ...(result?.map(() => ({
          type: QueryTags.AzdoConnectionTeam,
          id: arg.connectionId,
        })) ?? []),
      ],
    }),

    mapAzdoConnectionTeams: builder.mutation<
      void,
      AzdoConnectionTeamMappingsRequest
    >({
      queryFn: async (request) => {
        try {
          const data =
            await getAzureDevOpsConnectionsClient().mapConnectionTeams(
              request.connectionId,
              request,
            )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.AzdoConnectionTeam, id: arg.connectionId }]
      },
    }),

    testAzdoConfiguration: builder.mutation<
      void,
      TestAzureDevOpsConnectionRequest
    >({
      queryFn: async (request) => {
        try {
          const data =
            await getAzureDevOpsConnectionsClient().testConfig(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
  }),
})

export const {
  useSyncAzdoConnectionOrganizationMutation,
  useSetAzdoWorkspaceSyncMutation,
  useInitAzdoConnectionWorkspaceMutation,
  useGetAzdoConnectionTeamsQuery,
  useMapAzdoConnectionTeamsMutation,
  useTestAzdoConfigurationMutation,
} = azdoIntegrationApi
