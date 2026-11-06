import { getProjectDeliverablesClient } from '@/src/services/clients'
import {
  ChangeProjectDeliverableStatusRequest,
  CreateProjectDeliverableRequest,
  ProjectDeliverableDto,
  StatusOptionDto,
  StatusTransitionDto,
  UpdateProjectDeliverableRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { projectActivityTags } from './ppm-activity-tags'

export interface ProjectDeliverableRef {
  projectId: string
  deliverableId: string
}

/**
 * Every deliverable change also moves the project's `revision`, which the
 * next change sends back as `expectedRevision`, so the project is refetched
 * along with its deliverables and its activity.
 */
const afterChange = (projectId: string) => [
  { type: QueryTags.ProjectDeliverable, id: projectId },
  { type: QueryTags.Project, id: projectId },
  ...projectActivityTags(projectId),
]

export const projectDeliverablesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProjectDeliverables: builder.query<ProjectDeliverableDto[], string>({
      queryFn: async (projectId) => {
        try {
          const data =
            await getProjectDeliverablesClient().getDeliverables(projectId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, projectId) => [
        { type: QueryTags.ProjectDeliverable, id: projectId },
      ],
    }),

    getProjectDeliverableStatusOptions: builder.query<
      StatusOptionDto[],
      ProjectDeliverableRef
    >({
      queryFn: async ({ projectId, deliverableId }) => {
        try {
          const data = await getProjectDeliverablesClient().getStatusOptions(
            projectId,
            deliverableId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { projectId }) => [
        { type: QueryTags.ProjectDeliverable, id: projectId },
      ],
    }),

    getProjectDeliverableStatusHistory: builder.query<
      StatusTransitionDto[],
      ProjectDeliverableRef
    >({
      queryFn: async ({ projectId, deliverableId }) => {
        try {
          const data = await getProjectDeliverablesClient().getStatusHistory(
            projectId,
            deliverableId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { projectId }) => [
        { type: QueryTags.ProjectDeliverable, id: projectId },
      ],
    }),

    createProjectDeliverable: builder.mutation<
      string,
      { projectId: string; request: CreateProjectDeliverableRequest }
    >({
      queryFn: async ({ projectId, request }) => {
        try {
          const data = await getProjectDeliverablesClient().createDeliverable(
            projectId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId }) =>
        afterChange(projectId),
    }),

    updateProjectDeliverable: builder.mutation<
      void,
      ProjectDeliverableRef & { request: UpdateProjectDeliverableRequest }
    >({
      queryFn: async ({ projectId, deliverableId, request }) => {
        try {
          await getProjectDeliverablesClient().updateDeliverable(
            projectId,
            deliverableId,
            request,
          )
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId }) =>
        afterChange(projectId),
    }),

    changeProjectDeliverableStatus: builder.mutation<
      void,
      ProjectDeliverableRef & {
        request: ChangeProjectDeliverableStatusRequest
      }
    >({
      queryFn: async ({ projectId, deliverableId, request }) => {
        try {
          await getProjectDeliverablesClient().changeStatus(
            projectId,
            deliverableId,
            request,
          )
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId }) =>
        afterChange(projectId),
    }),

    reorderProjectDeliverables: builder.mutation<
      void,
      { projectId: string; deliverableIds: string[]; expectedRevision: number }
    >({
      queryFn: async ({ projectId, deliverableIds, expectedRevision }) => {
        try {
          await getProjectDeliverablesClient().reorderDeliverables(projectId, {
            deliverableIds,
            expectedRevision,
          })
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId }) =>
        afterChange(projectId),
    }),

    deleteProjectDeliverable: builder.mutation<
      void,
      ProjectDeliverableRef & { expectedRevision: number }
    >({
      queryFn: async ({ projectId, deliverableId, expectedRevision }) => {
        try {
          await getProjectDeliverablesClient().deleteDeliverable(
            projectId,
            deliverableId,
            expectedRevision,
          )
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId }) =>
        afterChange(projectId),
    }),
  }),
})

export const {
  useGetProjectDeliverablesQuery,
  useGetProjectDeliverableStatusOptionsQuery,
  useGetProjectDeliverableStatusHistoryQuery,
  useCreateProjectDeliverableMutation,
  useUpdateProjectDeliverableMutation,
  useChangeProjectDeliverableStatusMutation,
  useReorderProjectDeliverablesMutation,
  useDeleteProjectDeliverableMutation,
} = projectDeliverablesApi
