import {
  oidcProvidersCreate,
  oidcProvidersDelete,
  oidcProvidersGetActivities,
  oidcProvidersGetById,
  oidcProvidersGetList,
  oidcProvidersGetMigrationCandidates,
  oidcProvidersGetPendingMigrations,
  oidcProvidersStageBulkTenantMigration,
  oidcProvidersTestDiscovery,
  oidcProvidersUpdate,
} from '@/src/services/api/oidc-providers'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  BulkTenantMigrationResult,
  CreateOidcProviderRequest,
  OidcProviderDto,
  OidcProviderListItemDto,
  PagedResponseOfActivityLogDto,
  PendingTenantMigrationDto,
  StageBulkTenantMigrationRequest,
  TenantMigrationCandidateDto,
  TestOidcProviderDiscoveryResult,
  UpdateOidcProviderRequest,
} from '@/src/services/api/wayd.schemas'

export const oidcProvidersApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getOidcProviders: builder.query<OidcProviderListItemDto[], void>({
      queryFn: async () => {
        try {
          const data = await oidcProvidersGetList()
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
          const data = await oidcProvidersGetById(id)
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

    createOidcProvider: builder.mutation<
      OidcProviderDto,
      CreateOidcProviderRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await oidcProvidersCreate(request)
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
          const data = await oidcProvidersUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.OidcProvider, id: 'LIST' },
        { type: QueryTags.OidcProvider, id: arg.id },
        { type: QueryTags.ActivityLog, id: arg.id },
      ],
    }),

    deleteOidcProvider: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await oidcProvidersDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.OidcProvider, id: 'LIST' },
        { type: QueryTags.ActivityLog, id },
      ],
    }),

    getOidcProviderActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await oidcProvidersGetActivities(String(idOrKey), {
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

    testOidcProviderDiscovery: builder.mutation<
      TestOidcProviderDiscoveryResult,
      string
    >({
      queryFn: async (id) => {
        try {
          const data = await oidcProvidersTestDiscovery(id)
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
          const data = await oidcProvidersGetMigrationCandidates(providerId, {
            sourceTenantId: sourceTenantId,
          })
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

    getPendingTenantMigrations: builder.query<
      PendingTenantMigrationDto[],
      string
    >({
      queryFn: async (providerId) => {
        try {
          const data = await oidcProvidersGetPendingMigrations(providerId)
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
          const data = await oidcProvidersStageBulkTenantMigration(
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
        ...arg.request.userIds.map((id) => ({
          type: QueryTags.ActivityLog,
          id,
        })),
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
  useGetOidcProviderActivitiesQuery,
  useLazyGetOidcProviderActivitiesQuery,
  useTestOidcProviderDiscoveryMutation,
  useGetTenantMigrationCandidatesQuery,
  useGetPendingTenantMigrationsQuery,
  useStageBulkTenantMigrationMutation,
} = oidcProvidersApi
