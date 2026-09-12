import { getDeploymentsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  DeploymentDto,
  FailDeploymentRequest,
  ImportProcessDto,
  ObjectIdAndKey,
  RollBackDeploymentRequest,
  StartDeploymentRequest,
  StatusTransitionDto,
  SucceedDeploymentRequest,
  PagedResponseOfActivityLogDto,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetDeploymentsRequest {
  versionId?: string
  packageId?: string
  environmentId?: string
  environmentCategory?: number
  /**
   * ISO-8601, not a `Date`: query arguments become the Redux cache key, and a `Date` there is
   * non-serializable. Converted for the client in the queryFn below.
   */
  startedOnOrAfter?: string
}

/**
 * Recording an outcome also moves the delivery measures, which count completed production
 * deployments — so they are invalidated alongside the record itself.
 *
 * `cacheKey` is the deployment's short key, following the convention the app's other slices use, and
 * is required: a detail page queries its history by that key while a mutation holds only the id, so
 * invalidating one alone leaves the history stale.
 */
export const deploymentTags = (id: string, cacheKey: number) => [
  { type: QueryTags.Deployment, id: 'LIST' },
  { type: QueryTags.Deployment, id },
  { type: QueryTags.StatusHistory, id },
  { type: QueryTags.StatusHistory, id: String(cacheKey) },
  { type: QueryTags.DeliveryMetrics, id: 'LIST' },
]

export const deploymentsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getDeployments: builder.query<
      DeploymentDto[],
      GetDeploymentsRequest | undefined
    >({
      queryFn: async (request = {}) => {
        try {
          const data = await getDeploymentsClient().getDeployments(
            request.versionId,
            request.packageId,
            request.environmentId,
            request.environmentCategory,
            request.startedOnOrAfter
              ? new Date(request.startedOnOrAfter)
              : undefined,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Deployment, id: 'LIST' }],
    }),
    getDeployment: builder.query<DeploymentDto, string>({
      queryFn: async (id) => {
        try {
          const data = await getDeploymentsClient().getDeployment(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.Deployment, id: arg },
      ],
    }),
    getDeploymentStatusHistory: builder.query<StatusTransitionDto[], string>({
      queryFn: async (id) => {
        try {
          const data = await getDeploymentsClient().getStatusHistory(id)
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
    startDeployment: builder.mutation<ObjectIdAndKey, StartDeploymentRequest>({
      queryFn: async (request) => {
        try {
          const data = await getDeploymentsClient().start(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.Deployment, id: 'LIST' },
        { type: QueryTags.DeliveryMetrics, id: 'LIST' },
      ],
    }),
    // The generated client takes a FileParameter, so the caller hands over the browser File and its
    // name travels with it.
    importDeployments: builder.mutation<ImportProcessDto, File>({
      queryFn: async (file) => {
        try {
          // A file uploaded from the app is submitted on its own, under no group.
          const data = await getDeploymentsClient().import(undefined, {
            data: file,
            fileName: file.name,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // An import writes many deployments at once, so the list and the measures computed from it are
      // refetched rather than patched. They only have them if the run finished within the wait; one
      // still running refreshes them from its import page.
      invalidatesTags: () => [
        { type: QueryTags.Deployment, id: 'LIST' },
        { type: QueryTags.DeliveryMetrics, id: 'LIST' },
        QueryTags.ImportProcess,
      ],
    }),
    succeedDeployment: builder.mutation<
      void,
      { id: string; cacheKey: number; request: SucceedDeploymentRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getDeploymentsClient().succeed(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        deploymentTags(arg.id, arg.cacheKey),
    }),
    failDeployment: builder.mutation<
      void,
      { id: string; cacheKey: number; request: FailDeploymentRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getDeploymentsClient().fail(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        deploymentTags(arg.id, arg.cacheKey),
    }),
    rollBackDeployment: builder.mutation<
      void,
      { id: string; cacheKey: number; request: RollBackDeploymentRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getDeploymentsClient().rollBack(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        deploymentTags(arg.id, arg.cacheKey),
    }),

    getDeploymentActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await getDeploymentsClient().getActivities(
            String(idOrKey),
            page,
            pageSize,
          )
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
  }),
})

export const {
  useGetDeploymentsQuery,
  useGetDeploymentQuery,
  useGetDeploymentStatusHistoryQuery,
  useStartDeploymentMutation,
  useImportDeploymentsMutation,
  useSucceedDeploymentMutation,
  useFailDeploymentMutation,
  useRollBackDeploymentMutation,
  useGetDeploymentActivitiesQuery,
  useLazyGetDeploymentActivitiesQuery,
} = deploymentsApi
