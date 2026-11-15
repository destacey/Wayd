import {
  deploymentEnvironmentsCreate,
  deploymentEnvironmentsDelete,
  deploymentEnvironmentsGetDeploymentEnvironments,
  deploymentEnvironmentsGetRollout,
  deploymentEnvironmentsSetActive,
  deploymentEnvironmentsUpdate,
} from '@/src/services/api/deployment-environments'
import { apiSlice } from '../apiSlice'
import {
  CreateDeploymentEnvironmentRequest,
  DeploymentEnvironmentDto,
  EnvironmentCategory,
  EnvironmentRolloutDto,
  ObjectIdAndKey,
  SetDeploymentEnvironmentActiveRequest,
  UpdateDeploymentEnvironmentRequest,
} from '@/src/services/api/wayd.schemas'
import { QueryTags } from '../query-tags'

export interface GetDeploymentEnvironmentsRequest {
  isActive?: boolean
  category?: EnvironmentCategory
}

export const deploymentEnvironmentsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Environments, by ring then name.
     *
     * The list is the only read path — there is no single-environment endpoint — so a form editing
     * one takes the row it was opened from rather than refetching it.
     */
    getDeploymentEnvironments: builder.query<
      DeploymentEnvironmentDto[],
      GetDeploymentEnvironmentsRequest | undefined
    >({
      queryFn: async (request = {}) => {
        try {
          const data = await deploymentEnvironmentsGetDeploymentEnvironments({
            isActive: request.isActive,
            category: request.category,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [
        { type: QueryTags.DeploymentEnvironment, id: 'LIST' },
      ],
    }),
    /**
     * What each environment is running, derived from its deployments.
     *
     * Tagged against deployments as well as environments: the answer changes when a deployment
     * completes, not only when an environment is added or retired.
     */
    getEnvironmentRollout: builder.query<
      EnvironmentRolloutDto[],
      { includeInactive?: boolean } | undefined
    >({
      queryFn: async (request = {}) => {
        try {
          const data = await deploymentEnvironmentsGetRollout({
            includeInactive: request.includeInactive,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [
        { type: QueryTags.DeploymentEnvironment, id: 'LIST' },
        { type: QueryTags.Deployment, id: 'LIST' },
      ],
    }),
    createDeploymentEnvironment: builder.mutation<
      ObjectIdAndKey,
      CreateDeploymentEnvironmentRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await deploymentEnvironmentsCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.DeploymentEnvironment, id: 'LIST' },
      ],
    }),
    updateDeploymentEnvironment: builder.mutation<
      void,
      { id: string; request: UpdateDeploymentEnvironmentRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await deploymentEnvironmentsUpdate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.DeploymentEnvironment, id: 'LIST' },
      ],
    }),
    /**
     * Retires or reinstates an environment. Retiring keeps the deployments that point here, which is
     * why it is the everyday way out rather than deleting.
     */
    setDeploymentEnvironmentActive: builder.mutation<
      void,
      { id: string; request: SetDeploymentEnvironmentActiveRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await deploymentEnvironmentsSetActive(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.DeploymentEnvironment, id: 'LIST' },
      ],
    }),
    /**
     * Deletes an environment and every deployment into it, so the deployment lists and the delivery
     * measures are invalidated with it.
     */
    deleteDeploymentEnvironment: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await deploymentEnvironmentsDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.DeploymentEnvironment, id: 'LIST' },
        { type: QueryTags.Deployment, id: 'LIST' },
        { type: QueryTags.DeliveryMetrics, id: 'LIST' },
      ],
    }),
  }),
})

export const {
  useGetDeploymentEnvironmentsQuery,
  useGetEnvironmentRolloutQuery,
  useCreateDeploymentEnvironmentMutation,
  useUpdateDeploymentEnvironmentMutation,
  useDeleteDeploymentEnvironmentMutation,
  useSetDeploymentEnvironmentActiveMutation,
} = deploymentEnvironmentsApi
