import { getDeploymentEnvironmentsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CreateDeploymentEnvironmentRequest,
  DeploymentEnvironmentDto,
  ObjectIdAndKey,
  SetDeploymentEnvironmentActiveRequest,
  UpdateDeploymentEnvironmentRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetDeploymentEnvironmentsRequest {
  isActive?: boolean
  category?: number
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
          const data =
            await getDeploymentEnvironmentsClient().getDeploymentEnvironments(
              request.isActive,
              request.category,
            )
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
    createDeploymentEnvironment: builder.mutation<
      ObjectIdAndKey,
      CreateDeploymentEnvironmentRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getDeploymentEnvironmentsClient().create(request)
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
          const data = await getDeploymentEnvironmentsClient().update(id, request)
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
     * Retires or reinstates an environment.
     *
     * The only destructive action there is: historical deployments still point here, so an
     * environment is never deleted.
     */
    setDeploymentEnvironmentActive: builder.mutation<
      void,
      { id: string; request: SetDeploymentEnvironmentActiveRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getDeploymentEnvironmentsClient().setActive(
            id,
            request,
          )
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
  }),
})

export const {
  useGetDeploymentEnvironmentsQuery,
  useCreateDeploymentEnvironmentMutation,
  useUpdateDeploymentEnvironmentMutation,
  useSetDeploymentEnvironmentActiveMutation,
} = deploymentEnvironmentsApi
