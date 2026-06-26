import { getProjectHealthChecksClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CreateProjectHealthCheckRequest,
  ProjectHealthCheckDetailsDto,
  UpdateProjectHealthCheckRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface ProjectHealthCheckScope {
  projectId: string
}

export interface ProjectHealthCheckRef extends ProjectHealthCheckScope {
  healthCheckId: string
}

export interface CreateProjectHealthCheckArgs extends ProjectHealthCheckScope {
  request: CreateProjectHealthCheckRequest
}

export interface UpdateProjectHealthCheckArgs extends ProjectHealthCheckScope {
  request: UpdateProjectHealthCheckRequest & { id: string }
}

export const projectHealthChecksApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProjectHealthChecks: builder.query<
      ProjectHealthCheckDetailsDto[],
      ProjectHealthCheckScope
    >({
      queryFn: async ({ projectId }) => {
        try {
          const data = await getProjectHealthChecksClient().getHealthChecks(projectId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result = [], _error, { projectId }) => [
        { type: QueryTags.ProjectHealthChecksHealthReport, id: projectId },
        ...result.map(({ id }) => ({
          type: QueryTags.ProjectHealthCheck,
          id,
        })),
      ],
    }),

    getProjectHealthCheck: builder.query<
      ProjectHealthCheckDetailsDto,
      ProjectHealthCheckRef
    >({
      queryFn: async ({ projectId, healthCheckId }) => {
        try {
          const data = await getProjectHealthChecksClient().getHealthCheck(
            projectId,
            healthCheckId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { healthCheckId }) => [
        { type: QueryTags.ProjectHealthCheck, id: healthCheckId },
      ],
    }),

    createProjectHealthCheck: builder.mutation<
      string,
      CreateProjectHealthCheckArgs
    >({
      queryFn: async ({ projectId, request }) => {
        try {
          const data = await getProjectHealthChecksClient().createHealthCheck(
            projectId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId }) => [
        { type: QueryTags.ProjectHealthChecksHealthReport, id: projectId },
        { type: QueryTags.Project, id: 'LIST' },
        { type: QueryTags.Project, id: projectId },
      ],
    }),

    updateProjectHealthCheck: builder.mutation<
      ProjectHealthCheckDetailsDto,
      UpdateProjectHealthCheckArgs
    >({
      queryFn: async ({ projectId, request }) => {
        try {
          const data = await getProjectHealthChecksClient().updateHealthCheck(
            projectId,
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId, request }) => [
        { type: QueryTags.ProjectHealthCheck, id: request.id },
        { type: QueryTags.ProjectHealthChecksHealthReport, id: projectId },
        { type: QueryTags.Project, id: 'LIST' },
        { type: QueryTags.Project, id: projectId },
      ],
    }),

    deleteProjectHealthCheck: builder.mutation<void, ProjectHealthCheckRef>({
      queryFn: async ({ projectId, healthCheckId }) => {
        try {
          await getProjectHealthChecksClient().deleteHealthCheck(projectId, healthCheckId)
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error: error ?? new Error('Unknown error deleting health check') }
        }
      },
      invalidatesTags: (_result, _error, { projectId, healthCheckId }) => [
        { type: QueryTags.ProjectHealthCheck, id: healthCheckId },
        { type: QueryTags.ProjectHealthChecksHealthReport, id: projectId },
        { type: QueryTags.Project, id: 'LIST' },
        { type: QueryTags.Project, id: projectId },
      ],
    }),
  }),
})

export const {
  useGetProjectHealthChecksQuery,
  useGetProjectHealthCheckQuery,
  useCreateProjectHealthCheckMutation,
  useUpdateProjectHealthCheckMutation,
  useDeleteProjectHealthCheckMutation,
} = projectHealthChecksApi
