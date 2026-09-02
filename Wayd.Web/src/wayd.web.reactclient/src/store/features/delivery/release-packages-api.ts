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
  /** Packages naming this product in any version — what a component has ever shipped in. */
  containingProductId?: string
  /**
   * Packages naming this exact release. Narrower than `containingProductId`, and what a release's own
   * page needs: the product-wide filter would list packages that release was never part of.
   */
  containingReleaseId?: string
}

/**
 * Tags every query and mutation touches for one package.
 *
 * `cacheKey` is the package's short key, following the convention the app's other slices use, and is
 * required: a detail page queries its history by that key while a mutation holds only the id, so
 * invalidating one alone leaves the history stale.
 */
export const packageTags = (id: string, cacheKey: number) => [
  { type: QueryTags.ReleasePackage, id: 'LIST' },
  { type: QueryTags.ReleasePackage, id },
  { type: QueryTags.StatusHistory, id },
  { type: QueryTags.StatusHistory, id: String(cacheKey) },
]

export const releasePackagesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Packages, newest first.
     *
     * Both containing-* filters read the manifest, since a release records no package of its own:
     * `containingReleaseId` matches the one release (what a release's page asks), while
     * `containingProductId` matches any manifest line for that product, changed or carried forward.
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
            request.containingReleaseId,
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
      { id: string; cacheKey: number; request: SetReleasePackageManifestRequest }
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
      invalidatesTags: (result, error, arg) => packageTags(arg.id, arg.cacheKey),
    }),
    markReleasePackageReleased: builder.mutation<
      void,
      { id: string; cacheKey: number; request: MarkReleasePackageReleasedRequest }
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
      invalidatesTags: (result, error, arg) => packageTags(arg.id, arg.cacheKey),
    }),
    withdrawReleasePackage: builder.mutation<
      void,
      { id: string; cacheKey: number; request: WithdrawReleasePackageRequest }
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
      invalidatesTags: (result, error, arg) => packageTags(arg.id, arg.cacheKey),
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
