import {
  getProjectTasksClient,
  getProjectsClient,
  authenticatedFetch,
} from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  ProjectTaskListDto,
  ProjectTaskDto,
  CreateProjectTaskRequest,
  UpdateProjectTaskRequest,
  AddTaskDependencyRequest,
  ProjectTaskIdAndKey,
  UpdateProjectTaskPlacementRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'
import { OptionModel } from '@/src/components/types'

interface GetProjectTasksParams {
  projectIdOrKey: string
  status?: number
  parentId?: string
}

export const projectTasksApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProjectTasks: builder.query<ProjectTaskListDto[], GetProjectTasksParams>(
      {
        queryFn: async ({ projectIdOrKey, status, parentId }) => {
          try {
            const data = await getProjectTasksClient().getProjectTasks(
              projectIdOrKey,
              status,
              parentId,
            )
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        providesTags: (result, error, { projectIdOrKey }) => [
          { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        ],
      },
    ),

    getProjectTask: builder.query<
      ProjectTaskDto,
      { projectIdOrKey: string; taskIdOrKey: string }
    >({
      queryFn: async ({ projectIdOrKey, taskIdOrKey }) => {
        try {
          const data = await getProjectTasksClient().getProjectTask(
            projectIdOrKey,
            taskIdOrKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { taskIdOrKey }) => [
        { type: QueryTags.ProjectTask, id: taskIdOrKey },
      ],
    }),

    createProjectTask: builder.mutation<
      ProjectTaskIdAndKey,
      { projectIdOrKey: string; request: CreateProjectTaskRequest }
    >({
      queryFn: async ({ projectIdOrKey, request }) => {
        try {
          const data = await getProjectTasksClient().createProjectTask(
            projectIdOrKey,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { projectIdOrKey }) => [
        { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        { type: QueryTags.ProjectTaskTree, id: `TREE-${projectIdOrKey}` },
      ],
    }),

    updateProjectTask: builder.mutation<
      void,
      {
        projectIdOrKey: string
        request: UpdateProjectTaskRequest
        cacheKey: string
      }
    >({
      queryFn: async ({ projectIdOrKey, request }) => {
        try {
          const data = await getProjectTasksClient().updateProjectTask(
            projectIdOrKey,
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { projectIdOrKey, cacheKey }) => [
        { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        { type: QueryTags.ProjectTask, id: cacheKey },
        { type: QueryTags.ProjectTaskTree, id: `TREE-${projectIdOrKey}` },
      ],
    }),

    patchProjectTask: builder.mutation<
      void,
      {
        projectIdOrKey: string
        taskId: string
        patchOperations: Array<{
          op: 'replace' | 'add' | 'remove'
          path: string
          value?: any
        }>
        cacheKey: string
      }
    >({
      queryFn: async ({ projectIdOrKey, taskId, patchOperations }) => {
        try {
          const response = await authenticatedFetch(
            `/api/ppm/projects/${projectIdOrKey}/tasks/${taskId}`,
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
              errorData = {
                detail: await response.text(),
              }
            }

            return {
              error: {
                status: response.status,
                data: errorData,
              },
            }
          }

          return { data: null as any }
        } catch (error: any) {
          console.error('API Error:', error)
          return {
            error: {
              status: 'FETCH_ERROR',
              data: {
                detail:
                  error?.message ??
                  'An error occurred while updating the project task.',
              },
            },
          }
        }
      },
      invalidatesTags: (_result, _error, { projectIdOrKey, cacheKey }) => [
        { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        { type: QueryTags.ProjectTask, id: cacheKey },
        { type: QueryTags.ProjectTaskTree, id: `TREE-${projectIdOrKey}` },
      ],
    }),

    deleteProjectTask: builder.mutation<
      void,
      { projectIdOrKey: string; id: string }
    >({
      queryFn: async ({ projectIdOrKey, id }) => {
        try {
          const data = await getProjectTasksClient().deleteProjectTask(
            projectIdOrKey,
            id,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { projectIdOrKey }) => [
        { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        { type: QueryTags.ProjectTaskTree, id: `TREE-${projectIdOrKey}` },
      ],
    }),

    updateProjectTaskPlacement: builder.mutation<
      void,
      {
        projectIdOrKey: string
        id: string
        request: UpdateProjectTaskPlacementRequest
      }
    >({
      queryFn: async ({ projectIdOrKey, id, request }) => {
        try {
          const data = await getProjectTasksClient().updateProjectTaskPlacement(
            projectIdOrKey,
            id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { projectIdOrKey }) => [
        { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        { type: QueryTags.ProjectTaskTree, id: `TREE-${projectIdOrKey}` },
      ],
    }),

    getCriticalPath: builder.query<string[], { projectIdOrKey: string }>({
      queryFn: async ({ projectIdOrKey }) => {
        try {
          const data =
            await getProjectTasksClient().getCriticalPath(projectIdOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { projectIdOrKey }) => [
        { type: QueryTags.ProjectCriticalPath, id: projectIdOrKey },
      ],
    }),

    addTaskDependency: builder.mutation<
      void,
      {
        projectIdOrKey: string
        id: string
        request: AddTaskDependencyRequest
      }
    >({
      queryFn: async ({ projectIdOrKey, id, request }) => {
        try {
          const data = await getProjectTasksClient().addTaskDependency(
            projectIdOrKey,
            id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { projectIdOrKey, request }) => [
        { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        { type: QueryTags.ProjectTask, id: request.predecessorId },
        { type: QueryTags.ProjectTask, id: request.successorId },
        { type: QueryTags.ProjectTaskDependency, id: projectIdOrKey },
        { type: QueryTags.ProjectCriticalPath, id: projectIdOrKey },
      ],
    }),

    removeTaskDependency: builder.mutation<
      void,
      {
        projectIdOrKey: string
        id: string
        successorId: string
      }
    >({
      queryFn: async ({ projectIdOrKey, id, successorId }) => {
        try {
          const data = await getProjectTasksClient().removeTaskDependency(
            projectIdOrKey,
            id,
            successorId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { projectIdOrKey, id, successorId }) => [
        { type: QueryTags.ProjectTask, id: `LIST-${projectIdOrKey}` },
        { type: QueryTags.ProjectTask, id: id },
        { type: QueryTags.ProjectTask, id: successorId },
        { type: QueryTags.ProjectTaskDependency, id: projectIdOrKey },
        { type: QueryTags.ProjectCriticalPath, id: projectIdOrKey },
      ],
    }),

    getTaskStatusOptions: builder.query<
      OptionModel<number>[],
      { forMilestone?: boolean } | void
    >({
      queryFn: async (arg) => {
        try {
          const forMilestone =
            arg && typeof arg === 'object' ? arg.forMilestone : false
          const data = await getProjectTasksClient().getTaskStatuses()
          let options = data
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.id,
              label: s.name,
            }))

          // Filter out "In Progress" for milestones
          if (forMilestone) {
            options = options.filter((opt) => opt.label !== 'In Progress')
          }

          return { data: options }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, arg) => {
        const forMilestone =
          arg && typeof arg === 'object' ? arg.forMilestone : false
        return [
          {
            type: QueryTags.TaskStatusOptions,
            id: forMilestone ? 'MILESTONE' : 'LIST',
          },
        ]
      },
    }),

    getTaskPriorityOptions: builder.query<OptionModel<number>[], void>({
      queryFn: async () => {
        try {
          const data = await getProjectTasksClient().getTaskPriorities()
          return {
            data: data
              .sort((a, b) => a.order - b.order)
              .map((p) => ({
                value: p.id,
                label: p.name,
              })),
          }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.TaskPriorityOptions, id: 'LIST' }],
    }),

    getTaskTypeOptions: builder.query<OptionModel<number>[], void>({
      queryFn: async () => {
        try {
          const data = await getProjectTasksClient().getTaskTypes()
          return {
            data: data
              .sort((a, b) => a.order - b.order)
              .map((t) => ({
                value: t.id,
                label: t.name,
              })),
          }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.TaskTypeOptions, id: 'LIST' }],
    }),

    getParentTaskOptions: builder.query<
      Array<{
        value: string
        title: string
        selectable?: boolean
        children?: Array<{
          value: string
          title: string
          children?: any[]
        }>
      }>,
      { projectIdOrKey: string; excludeTaskId?: string }
    >({
      queryFn: async ({ projectIdOrKey, excludeTaskId }) => {
        try {
          const planData =
            await getProjectsClient().getProjectPlanTree(projectIdOrKey)

          // Convert task nodes to TreeSelect format, excluding milestones and a specific task
          const convertTasksToTreeSelect = (
            nodes: any[],
          ): Array<{
            value: string
            title: string
            children?: any[]
          }> => {
            return nodes
              .filter(
                (t: any) =>
                  t.id !== excludeTaskId &&
                  t.nodeType !== 'Stage' &&
                  t.type?.name !== 'Milestone',
              )
              .map((t: any) => ({
                value: t.id,
                title: `${t.key} - ${t.name}`,
                children:
                  t.children && t.children.length > 0
                    ? convertTasksToTreeSelect(t.children)
                    : undefined,
              }))
          }

          // Build tree with stages as top-level selectable parents
          const data = planData.map((node: any) => {
            if (node.nodeType === 'Stage') {
              return {
                value: node.id,
                title: node.name,
                children:
                  node.children && node.children.length > 0
                    ? convertTasksToTreeSelect(node.children)
                    : undefined,
              }
            }
            // Non-stage root nodes (shouldn't happen with lifecycle, but handle gracefully)
            return {
              value: node.id,
              title: `${node.key} - ${node.name}`,
              children:
                node.children && node.children.length > 0
                  ? convertTasksToTreeSelect(node.children)
                  : undefined,
            }
          })

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { projectIdOrKey }) => [
        { type: QueryTags.ProjectPlanTree, id: projectIdOrKey },
      ],
    }),
  }),
})

export const {
  useGetProjectTasksQuery,
  useGetProjectTaskQuery,
  useCreateProjectTaskMutation,
  useUpdateProjectTaskMutation,
  usePatchProjectTaskMutation,
  useDeleteProjectTaskMutation,
  useUpdateProjectTaskPlacementMutation,
  useGetCriticalPathQuery,
  useAddTaskDependencyMutation,
  useRemoveTaskDependencyMutation,
  useGetTaskStatusOptionsQuery,
  useGetTaskPriorityOptionsQuery,
  useGetTaskTypeOptionsQuery,
  useGetParentTaskOptionsQuery,
} = projectTasksApi
