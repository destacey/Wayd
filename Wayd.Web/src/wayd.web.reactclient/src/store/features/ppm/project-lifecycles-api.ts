import { getProjectLifecyclesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateProjectLifecycleRequest,
  ProjectLifecycleDetailsDto,
  ProjectLifecycleListDto,
  ProjectLifecycleStageRequest,
  ProjectLifecycleState,
  UpdateProjectLifecycleRequest,
} from '@/src/services/wayd-api'

export const projectLifecyclesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProjectLifecycles: builder.query<
      ProjectLifecycleListDto[],
      ProjectLifecycleState | null | undefined
    >({
      queryFn: async (state) => {
        try {
          const data =
            await getProjectLifecyclesClient().getProjectLifecycles(state)
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
          const data =
            await getProjectLifecyclesClient().getProjectLifecycle(idOrKey)
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
          const data = await getProjectLifecyclesClient().create(request)
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
          const data = await getProjectLifecyclesClient().update(
            request.id,
            request,
          )
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
          const data = await getProjectLifecyclesClient().delete(id)
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
          const data = await getProjectLifecyclesClient().activate(id)
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
          const data = await getProjectLifecyclesClient().archive(id)
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
          const data = await getProjectLifecyclesClient().addStage(
            lifecycleId,
            request,
          )
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
          const data = await getProjectLifecyclesClient().updateStage(
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
          const data = await getProjectLifecyclesClient().removeStage(
            lifecycleId,
            stageId,
          )
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
          const data = await getProjectLifecyclesClient().reorderStages(
            lifecycleId,
            { orderedStageIds },
          )
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
