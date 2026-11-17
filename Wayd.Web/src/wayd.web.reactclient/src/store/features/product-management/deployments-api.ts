import { toInstant } from '@/src/utils/instant'
import {
  deploymentsDelete,
  deploymentsFail,
  deploymentsGetActivities,
  deploymentsGetDeployment,
  deploymentsGetDeployments,
  deploymentsGetStatusHistory,
  deploymentsGetStatusWorkflow,
  deploymentsRollBack,
  deploymentsStart,
  deploymentsSucceed,
} from '@/src/services/api/deployments'
import { apiSlice } from '../apiSlice'
import {
  DeploymentDto,
  EnvironmentCategory,
  FailDeploymentRequest,
  ObjectIdAndKey,
  RollBackDeploymentRequest,
  StartDeploymentRequest,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  SucceedDeploymentRequest,
  PagedResponseOfActivityLogDto,
} from '@/src/services/api/wayd.schemas'
import { QueryTags } from '../query-tags'

export interface GetDeploymentsRequest {
  versionId?: string
  packageId?: string
  environmentId?: string
  environmentCategory?: EnvironmentCategory
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
          const data = await deploymentsGetDeployments({
            versionId: request.versionId,
            packageId: request.packageId,
            environmentId: request.environmentId,
            environmentCategory: request.environmentCategory,
            startedOnOrAfter: request.startedOnOrAfter
              ? toInstant(new Date(request.startedOnOrAfter))
              : undefined,
          })
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
          const data = await deploymentsGetDeployment(id)
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
          const data = await deploymentsGetStatusHistory(id)
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
    /**
     * The workflow the deployment moves through, to draw its history on. Tagged with the history too:
     * a reassignment records a transition, which is what moves the deployment onto another workflow.
     */
    getDeploymentStatusWorkflow: builder.query<
      StatusWorkflowDetailsDto,
      string
    >({
      queryFn: async (idOrKey) => {
        try {
          const data = await deploymentsGetStatusWorkflow(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        ...(result ? [{ type: QueryTags.StatusWorkflow, id: result.id }] : []),
        { type: QueryTags.StatusHistory, id: arg },
      ],
    }),
    startDeployment: builder.mutation<ObjectIdAndKey, StartDeploymentRequest>({
      queryFn: async (request) => {
        try {
          const data = await deploymentsStart(request)
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
    succeedDeployment: builder.mutation<
      void,
      { id: string; cacheKey: number; request: SucceedDeploymentRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await deploymentsSucceed(id, request)
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
          const data = await deploymentsFail(id, request)
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
          const data = await deploymentsRollBack(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        deploymentTags(arg.id, arg.cacheKey),
    }),

    // Only lists: invalidating the deleted record's own tags refetches its page, still mounted until
    // the redirect lands, and every one of those queries 404s.
    deleteDeployment: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await deploymentsDelete(id)
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

    getDeploymentActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await deploymentsGetActivities(String(idOrKey), {
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
  }),
})

export const {
  useGetDeploymentsQuery,
  useGetDeploymentQuery,
  useGetDeploymentStatusHistoryQuery,
  useGetDeploymentStatusWorkflowQuery,
  useStartDeploymentMutation,
  useSucceedDeploymentMutation,
  useFailDeploymentMutation,
  useRollBackDeploymentMutation,
  useDeleteDeploymentMutation,
  useGetDeploymentActivitiesQuery,
  useLazyGetDeploymentActivitiesQuery,
} = deploymentsApi
