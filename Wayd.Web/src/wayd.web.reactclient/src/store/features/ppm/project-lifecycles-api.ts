import {
  projectLifecyclesActivate,
  projectLifecyclesAddStage,
  projectLifecyclesArchive,
  projectLifecyclesCreate,
  projectLifecyclesDelete,
  projectLifecyclesGetProjectLifecycle,
  projectLifecyclesGetProjectLifecycles,
  projectLifecyclesRemoveStage,
  projectLifecyclesReorderStages,
  projectLifecyclesUpdate,
  projectLifecyclesUpdateStage,
} from '@/src/services/api/project-lifecycles'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateProjectLifecycleRequest,
  ProjectLifecycleDetailsDto,
  ProjectLifecycleListDto,
  ProjectLifecycleStageRequest,
  ProjectLifecycleState,
  UpdateProjectLifecycleRequest,
} from '@/src/services/api/wayd.schemas'

export const projectLifecyclesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProjectLifecycles: builder.query<
      ProjectLifecycleListDto[],
      ProjectLifecycleState | null | undefined
    >({
      queryFn: async (state) => {
        try {
          const data = await projectLifecyclesGetProjectLifecycles({
            state: state ?? undefined,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [
        { type: QueryTags.ProjectLifecycle },
      ],
    }),
    getProjectLifecycle: builder.query<ProjectLifecycleDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await projectLifecyclesGetProjectLifecycle(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ProjectLifecycle, id: arg },
      ],
    }),
    createProjectLifecycle: builder.mutation<
      string,
      CreateProjectLifecycleRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await projectLifecyclesCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [{ type: QueryTags.ProjectLifecycle, id: 'LIST' }]
      },
    }),
    updateProjectLifecycle: builder.mutation<
      void,
      { id: string } & UpdateProjectLifecycleRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await projectLifecyclesUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.ProjectLifecycle, id: 'LIST' },
          { type: QueryTags.ProjectLifecycle, id: arg.id },
        ]
      },
    }),
    deleteProjectLifecycle: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await projectLifecyclesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [{ type: QueryTags.ProjectLifecycle, id: 'LIST' }]
      },
    }),
    activateProjectLifecycle: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await projectLifecyclesActivate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.ProjectLifecycle, id: 'LIST' },
          { type: QueryTags.ProjectLifecycle, id: arg },
        ]
      },
    }),
    archiveProjectLifecycle: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await projectLifecyclesArchive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.ProjectLifecycle, id: 'LIST' },
          { type: QueryTags.ProjectLifecycle, id: arg },
        ]
      },
    }),
    addProjectLifecycleStage: builder.mutation<
      string,
      { lifecycleId: string } & ProjectLifecycleStageRequest
    >({
      queryFn: async ({ lifecycleId, ...request }) => {
        try {
          const data = await projectLifecyclesAddStage(lifecycleId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProjectLifecycle },
      ],
    }),
    updateProjectLifecycleStage: builder.mutation<
      void,
      { lifecycleId: string; stageId: string } & ProjectLifecycleStageRequest
    >({
      queryFn: async ({ lifecycleId, stageId, ...request }) => {
        try {
          const data = await projectLifecyclesUpdateStage(
            lifecycleId,
            stageId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ProjectLifecycle }],
    }),
    removeProjectLifecycleStage: builder.mutation<
      void,
      { lifecycleId: string; stageId: string }
    >({
      queryFn: async ({ lifecycleId, stageId }) => {
        try {
          const data = await projectLifecyclesRemoveStage(lifecycleId, stageId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProjectLifecycle },
      ],
    }),
    reorderProjectLifecycleStages: builder.mutation<
      void,
      { lifecycleId: string; orderedStageIds: string[] }
    >({
      queryFn: async ({ lifecycleId, orderedStageIds }) => {
        try {
          const data = await projectLifecyclesReorderStages(lifecycleId, {
            orderedStageIds,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProjectLifecycle },
      ],
    }),
  }),
})

export const {
  useGetProjectLifecyclesQuery,
  useGetProjectLifecycleQuery,
  useCreateProjectLifecycleMutation,
  useUpdateProjectLifecycleMutation,
  useDeleteProjectLifecycleMutation,
  useActivateProjectLifecycleMutation,
  useArchiveProjectLifecycleMutation,
  useAddProjectLifecycleStageMutation,
  useUpdateProjectLifecycleStageMutation,
  useRemoveProjectLifecycleStageMutation,
  useReorderProjectLifecycleStagesMutation,
} = projectLifecyclesApi
