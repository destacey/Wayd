import { getDeploymentsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  DeploymentDto,
  FailDeploymentRequest,
  ObjectIdAndKey,
  RollBackDeploymentRequest,
  StartDeploymentRequest,
  StatusTransitionDto,
  SucceedDeploymentRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetDeploymentsRequest {
  releaseId?: string
  packageId?: string
  environmentId?: string
  environmentCategory?: number
  startedOnOrAfter?: Date
}

/**
 * Recording an outcome also moves the delivery measures, which count completed production
 * deployments — so they are invalidated alongside the record itself.
 */
const deploymentTags = (id: string) => [
  { type: QueryTags.Deployment, id: 'LIST' },
  { type: QueryTags.Deployment, id },
  { type: QueryTags.StatusHistory, id },
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
            request.releaseId,
            request.packageId,
            request.environmentId,
            request.environmentCategory,
            request.startedOnOrAfter,
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
    succeedDeployment: builder.mutation<
      void,
      { id: string; request: SucceedDeploymentRequest }
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
      invalidatesTags: (result, error, arg) => deploymentTags(arg.id),
    }),
    failDeployment: builder.mutation<
      void,
      { id: string; request: FailDeploymentRequest }
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
      invalidatesTags: (result, error, arg) => deploymentTags(arg.id),
    }),
    rollBackDeployment: builder.mutation<
      void,
      { id: string; request: RollBackDeploymentRequest }
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
      invalidatesTags: (result, error, arg) => deploymentTags(arg.id),
    }),
  }),
})

export const {
  useGetDeploymentsQuery,
  useGetDeploymentQuery,
  useGetDeploymentStatusHistoryQuery,
  useStartDeploymentMutation,
  useSucceedDeploymentMutation,
  useFailDeploymentMutation,
  useRollBackDeploymentMutation,
} = deploymentsApi
