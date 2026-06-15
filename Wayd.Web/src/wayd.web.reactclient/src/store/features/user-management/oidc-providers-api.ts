import { getOidcProvidersClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  BulkTenantMigrationResult,
  CreateOidcProviderRequest,
  OidcProviderDto,
  OidcProviderListItemDto,
  PendingTenantMigrationDto,
  StageBulkTenantMigrationRequest,
  TenantMigrationCandidateDto,
  TestOidcProviderDiscoveryResult,
  UpdateOidcProviderRequest,
} from '@/src/services/wayd-api'

export const oidcProvidersApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getOidcProviders: builder.query<OidcProviderListItemDto[], void>({
      queryFn: async () => {
        try {
          const data = await getOidcProvidersClient().getList()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.OidcProvider, id: 'LIST' }],
    }),

    getOidcProvider: builder.query<OidcProviderDto, string>({
      queryFn: async (id) => {
        try {
          const data = await getOidcProvidersClient().getById(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        { type: QueryTags.OidcProvider, id: result?.id },
      ],
    }),

    createOidcProvider: builder.mutation<OidcProviderDto, CreateOidcProviderRequest>({
      queryFn: async (request) => {
        try {
          const data = await getOidcProvidersClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.OidcProvider, id: 'LIST' }],
    }),

    updateOidcProvider: builder.mutation<void, UpdateOidcProviderRequest>({
      queryFn: async (request) => {
        try {
          const data = await getOidcProvidersClient().update(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.OidcProvider, id: 'LIST' },
        { type: QueryTags.OidcProvider, id: arg.id },
      ],
    }),

    deleteOidcProvider: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getOidcProvidersClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.OidcProvider, id: 'LIST' }],
    }),

    testOidcProviderDiscovery: builder.mutation<TestOidcProviderDiscoveryResult, string>({
      queryFn: async (id) => {
        try {
          const data = await getOidcProvidersClient().testDiscovery(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    getTenantMigrationCandidates: builder.query<
      TenantMigrationCandidateDto[],
      { providerId: string; sourceTenantId: string }
    >({
      queryFn: async ({ providerId, sourceTenantId }) => {
        try {
          const data = await getOidcProvidersClient().getMigrationCandidates(
            providerId,
            sourceTenantId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.TenantMigrationCandidates, id: arg.providerId },
      ],
    }),

    getPendingTenantMigrations: builder.query<PendingTenantMigrationDto[], string>({
      queryFn: async (providerId) => {
        try {
          const data =
            await getOidcProvidersClient().getPendingMigrations(providerId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, providerId) => [
        { type: QueryTags.PendingTenantMigrations, id: providerId },
      ],
    }),

    stageBulkTenantMigration: builder.mutation<
      BulkTenantMigrationResult,
      { providerId: string; request: StageBulkTenantMigrationRequest }
    >({
      queryFn: async ({ providerId, request }) => {
        try {
          const data = await getOidcProvidersClient().stageBulkTenantMigration(
            providerId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Staging moves users from the candidate list onto the pending list, and
      // changes their pendingMigrationTenantId on the user records.
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.TenantMigrationCandidates, id: arg.providerId },
        { type: QueryTags.PendingTenantMigrations, id: arg.providerId },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserOption, id: 'LIST' },
      ],
    }),
  }),
})

export const {
  useGetOidcProvidersQuery,
  useGetOidcProviderQuery,
  useCreateOidcProviderMutation,
  useUpdateOidcProviderMutation,
  useDeleteOidcProviderMutation,
  useTestOidcProviderDiscoveryMutation,
  useGetTenantMigrationCandidatesQuery,
  useGetPendingTenantMigrationsQuery,
  useStageBulkTenantMigrationMutation,
} = oidcProvidersApi
