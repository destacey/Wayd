import { getReleasePackagesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  AssembleReleasePackageRequest,
  MarkReleasePackageReleasedRequest,
  ObjectIdAndKey,
  ReleasePackageDto,
  SetReleasePackageManifestRequest,
  StatusTransitionDto,
  WithdrawReleasePackageRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetReleasePackagesRequest {
  statusCategory?: number[]
  containingProductId?: string
}

const packageTags = (id: string) => [
  { type: QueryTags.ReleasePackage, id: 'LIST' },
  { type: QueryTags.ReleasePackage, id },
  { type: QueryTags.StatusHistory, id },
]

export const releasePackagesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Packages, newest first.
     *
     * `containingProductId` matches any manifest line for that product, changed or carried forward —
     * which is how a release finds the packages that shipped it, since the release itself records no
     * package.
     */
    getReleasePackages: builder.query<
      ReleasePackageDto[],
      GetReleasePackagesRequest | undefined
    >({
      queryFn: async (request = {}) => {
        try {
          const data = await getReleasePackagesClient().getReleasePackages(
            request.statusCategory,
            request.containingProductId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.ReleasePackage, id: 'LIST' }],
    }),
    getReleasePackage: builder.query<ReleasePackageDto, string>({
      queryFn: async (id) => {
        try {
          const data = await getReleasePackagesClient().getReleasePackage(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ReleasePackage, id: arg },
      ],
    }),
    getReleasePackageStatusHistory: builder.query<StatusTransitionDto[], string>({
      queryFn: async (id) => {
        try {
          const data = await getReleasePackagesClient().getStatusHistory(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.StatusHistory, id: arg },
      ],
    }),
    assembleReleasePackage: builder.mutation<
      ObjectIdAndKey,
      AssembleReleasePackageRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getReleasePackagesClient().assemble(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ReleasePackage, id: 'LIST' }],
    }),
    /**
     * Replaces the whole manifest.
     *
     * Components carry no id and cannot be addressed individually, so an edit sends every line the
     * package should end up with — not a delta.
     */
    setReleasePackageManifest: builder.mutation<
      void,
      { id: string; request: SetReleasePackageManifestRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getReleasePackagesClient().setManifest(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => packageTags(arg.id),
    }),
    markReleasePackageReleased: builder.mutation<
      void,
      { id: string; request: MarkReleasePackageReleasedRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getReleasePackagesClient().markReleased(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => packageTags(arg.id),
    }),
    withdrawReleasePackage: builder.mutation<
      void,
      { id: string; request: WithdrawReleasePackageRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getReleasePackagesClient().withdraw(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => packageTags(arg.id),
    }),
  }),
})

export const {
  useGetReleasePackagesQuery,
  useGetReleasePackageQuery,
  useGetReleasePackageStatusHistoryQuery,
  useAssembleReleasePackageMutation,
  useSetReleasePackageManifestMutation,
  useMarkReleasePackageReleasedMutation,
  useWithdrawReleasePackageMutation,
} = releasePackagesApi
