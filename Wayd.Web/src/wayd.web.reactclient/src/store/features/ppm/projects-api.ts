import { authenticatedFetch, getProjectsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CreateProjectRequest,
  ObjectIdAndKey,
  ProjectListDto,
  ProjectDetailsDto,
  UpdateProjectRequest,
  WorkItemListDto,
  ChangeProjectProgramRequest,
  ChangeProjectKeyRequest,
  AssignProjectLifecycleRequest,
  ProjectPlanNodeDto,
  ProjectPhaseDetailsDto,
  ProjectPlanSummaryDto,
  ProjectTeamMemberDto,
  MyProjectsSummaryDto,
  MyProjectsTaskMetricsDto,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'
import { BaseOptionType } from 'antd/es/select'
import { OptionModel } from '@/src/components/types'

export interface GetProjectsRequest {
  status?: number[]
  portfolioId?: string
  role?: number[]
}

export const projectsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProjects: builder.query<
      ProjectListDto[],
      GetProjectsRequest | undefined
    >({
      queryFn: async (request = undefined) => {
        try {
          const data = await getProjectsClient().getProjects(
            request?.status,
            request?.portfolioId,
            request?.role,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Project, id: 'LIST' }],
    }),

    getProject: builder.query<ProjectDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getProjectsClient().getProject(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Tag by both the resolved GUID id and the original arg (key). The detail page fetches by key,
      // while mutations such as recording a score invalidate by id — without the id tag those would
      // never refetch, leaving denormalized fields (e.g. currentScore) stale.
      providesTags: (result, error, arg) => [
        ...(result ? [{ type: QueryTags.Project, id: result.id }] : []),
        { type: QueryTags.Project, id: arg },
      ],
    }),

    createProject: builder.mutation<ObjectIdAndKey, CreateProjectRequest>({
      queryFn: async (request) => {
        try {
          const data = await getProjectsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    updateProject: builder.mutation<
      void,
      { request: UpdateProjectRequest; cacheKey: string }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await getProjectsClient().update(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: cacheKey },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    changeProjectProgram: builder.mutation<
      void,
      { id: string; request: ChangeProjectProgramRequest; cacheKey: string }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProjectsClient().changeProgram(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: cacheKey },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    changeProjectKey: builder.mutation<
      void,
      { id: string; request: ChangeProjectKeyRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProjectsClient().changeKey(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { request }) => {
        if (error) return []
        return [
          { type: QueryTags.Project, id: 'LIST' },
          // If any screens already cached the *new* key, ensure it's refreshed.
          { type: QueryTags.Project, id: request.key },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    approveProject: builder.mutation<void, { id: string; cacheKey: string }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getProjectsClient().approve(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: cacheKey },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    activateProject: builder.mutation<void, { id: string; cacheKey: string }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getProjectsClient().activate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: cacheKey },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    completeProject: builder.mutation<void, { id: string; cacheKey: string }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getProjectsClient().complete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: cacheKey },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    cancelProject: builder.mutation<void, { id: string; cacheKey: string }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getProjectsClient().cancel(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: cacheKey },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    deleteProject: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getProjectsClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    getProjectWorkItems: builder.query<WorkItemListDto[], string>({
      queryFn: async (id) => {
        try {
          const data = await getProjectsClient().getProjectWorkItems(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkItem,
        ...(result?.map(({ key }) => ({ type: QueryTags.ProjectWorkItems, key })) ?? []),
      ],
    }),

    getProjectStatusOptions: builder.query<OptionModel<number>[], void>({
      queryFn: async () => {
        try {
          const statuses = await getProjectsClient().getProjectStatuses()

          const data: OptionModel<number>[] = statuses
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.id,
              label: s.name,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.ProjectStatusOptions],
    }),

    getProjectOptions: builder.query<BaseOptionType[], void>({
      queryFn: async () => {
        try {
          const portfolios = await getProjectsClient().getProjects(undefined)

          const data: BaseOptionType[] = portfolios
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((category) => ({
              label: `${category.name} (${category.key})`,
              value: category.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    assignProjectLifecycle: builder.mutation<
      void,
      { id: string; request: AssignProjectLifecycleRequest; cacheKey: string }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProjectsClient().assignLifecycle(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: cacheKey },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    getProjectPlanTree: builder.query<ProjectPlanNodeDto[], string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getProjectsClient().getProjectPlanTree(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ProjectPlanTree, id: arg },
      ],
    }),

    getProjectPhase: builder.query<
      ProjectPhaseDetailsDto,
      { projectId: string; phaseId: string }
    >({
      queryFn: async ({ projectId, phaseId }) => {
        try {
          const data = await getProjectsClient().getProjectPhase(
            projectId,
            phaseId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    patchProjectPhase: builder.mutation<
      void,
      {
        projectId: string
        projectKey: string
        phaseId: string
        patchOperations: Array<{
          op: 'replace' | 'add' | 'remove'
          path: string
          value?: any
        }>
      }
    >({
      queryFn: async ({ projectId, phaseId, patchOperations }) => {
        try {
          const response = await authenticatedFetch(
            `/api/ppm/projects/${projectId}/phases/${phaseId}`,
            {
              method: 'PATCH',
              headers: {
                'Content-Type': 'application/json-patch+json',
              },
              body: JSON.stringify(patchOperations),
            },
          )

          if (!response.ok) {
            let errorData: unknown
            try {
              errorData = await response.json()
            } catch {
              errorData = { detail: await response.text() }
            }
            return { error: { status: response.status, data: errorData } }
          }

          return { data: null as any }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { projectId, projectKey }) => {
        return [
          { type: QueryTags.ProjectPlanTree, id: projectKey },
          { type: QueryTags.Project, id: 'LIST' },
          { type: QueryTags.Project, id: projectId },
          { type: QueryTags.Project, id: projectKey },
          { type: QueryTags.Project, id: 'MY_SUMMARY' },
          { type: QueryTags.PortfolioProjects, id: 'LIST' },
          { type: QueryTags.ProgramProjects, id: 'LIST' },
        ]
      },
    }),

    changeProjectLifecycle: builder.mutation<
      void,
      {
        projectId: string
        request: {
          lifecycleId: string
          phaseMapping: Record<string, string>
        }
      }
    >({
      queryFn: async ({ projectId, request }) => {
        try {
          const data = await getProjectsClient().changeProjectLifecycle(
            projectId,
            request as any,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.Project },
        { type: QueryTags.ProjectPlanTree },
      ],
    }),

    getProjectPlanSummary: builder.query<
      ProjectPlanSummaryDto,
      { projectKey: string; employeeId?: string }
    >({
      queryFn: async ({ projectKey, employeeId }) => {
        try {
          const data = await getProjectsClient().getProjectPlanSummary(
            projectKey,
            employeeId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ProjectPlanTree, id: arg.projectKey },
      ],
    }),

    getProjectsPlanSummaries: builder.query<
      Record<string, ProjectPlanSummaryDto>,
      { projectIds: string[]; role?: number[] }
    >({
      queryFn: async ({ projectIds, role }) => {
        try {
          const data = await getProjectsClient().getProjectsPlanSummaries(
            projectIds,
            role,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        ...arg.projectIds.map((id) => ({
          type: QueryTags.ProjectPlanTree as const,
          id,
        })),
      ],
    }),

    getMyProjectsSummary: builder.query<
      MyProjectsSummaryDto,
      { status?: number[] } | void
    >({
      queryFn: async (request = undefined) => {
        try {
          const status =
            request && 'status' in request ? request.status : undefined
          const data = await getProjectsClient().getMyProjectsSummary(status)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Project, id: 'MY_SUMMARY' }],
    }),

    getMyProjectsTaskMetrics: builder.query<
      MyProjectsTaskMetricsDto,
      { status?: number[]; role?: number[] } | void
    >({
      queryFn: async (request = undefined) => {
        try {
          const status =
            request && 'status' in request ? request.status : undefined
          const role =
            request && 'role' in request ? request.role : undefined
          const data =
            await getProjectsClient().getMyProjectsTaskMetrics(status, role)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Project, id: 'MY_TASK_METRICS' }],
    }),

    getProjectTeam: builder.query<ProjectTeamMemberDto[], string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getProjectsClient().getProjectTeam(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, idOrKey) => [
        { type: QueryTags.Project, id: `TEAM-${idOrKey}` },
      ],
    }),
  }),
})

export const {
  useGetProjectsQuery,
  useGetProjectQuery,
  useCreateProjectMutation,
  useUpdateProjectMutation,
  useChangeProjectProgramMutation,
  useChangeProjectKeyMutation,
  useApproveProjectMutation,
  useActivateProjectMutation,
  useCompleteProjectMutation,
  useCancelProjectMutation,
  useDeleteProjectMutation,
  useGetProjectWorkItemsQuery,
  useGetProjectOptionsQuery,
  useGetProjectStatusOptionsQuery,
  useAssignProjectLifecycleMutation,
  useGetProjectPlanTreeQuery,
  useGetProjectPhaseQuery,
  usePatchProjectPhaseMutation,
  useChangeProjectLifecycleMutation,
  useGetProjectPlanSummaryQuery,
  useGetProjectsPlanSummariesQuery,
  useGetMyProjectsSummaryQuery,
  useGetMyProjectsTaskMetricsQuery,
  useGetProjectTeamQuery,
} = projectsApi
