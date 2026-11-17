import {
  AddWorkflowStatusRequest,
  CloneStatusWorkflowRequest,
  CreateStatusWorkflowRequest,
  DescribeWorkflowStatusRequest,
  ReassignWorkflowRequest,
  ReclassifyWorkflowStatusRequest,
  RenameWorkflowStatusRequest,
  ReorderWorkflowStatusesRequest,
  SetWorkflowTransitionModeRequest,
  SetWorkflowTransitionsRequest,
  StatusRemapPreviewDto,
  StatusWorkflowDetailsDto,
  StatusWorkflowListDto,
  StatusWorkflowState,
  UpdateStatusWorkflowRequest,
  WorkflowAssignmentDto,
  WorkflowOwnerTypeDto,
  PagedResponseOfActivityLogDto,
} from '@/src/services/api/wayd.schemas'
import {
  statusWorkflowsAddStatus,
  statusWorkflowsArchive,
  statusWorkflowsClone,
  statusWorkflowsCreate,
  statusWorkflowsDescribeStatus,
  statusWorkflowsGetActivities,
  statusWorkflowsGetOwnerTypes,
  statusWorkflowsGetStatusWorkflow,
  statusWorkflowsGetStatusWorkflows,
  statusWorkflowsPublish,
  statusWorkflowsReclassifyStatus,
  statusWorkflowsRemoveStatus,
  statusWorkflowsRenameStatus,
  statusWorkflowsReorderStatuses,
  statusWorkflowsSetTransitionMode,
  statusWorkflowsSetTransitions,
  statusWorkflowsUpdate,
} from '@/src/services/api/status-workflows'
import {
  workflowAssignmentsGetWorkflowAssignments,
  workflowAssignmentsPreviewRemap,
  workflowAssignmentsReassign,
} from '@/src/services/api/workflow-assignments'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

export const statusWorkflowsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getStatusWorkflows: builder.query<
      StatusWorkflowListDto[],
      { ownerType?: string; state?: StatusWorkflowState } | undefined
    >({
      queryFn: async (args) => {
        try {
          const data = await statusWorkflowsGetStatusWorkflows({
            ownerType: args?.ownerType,
            state: args?.state,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.StatusWorkflow, id: 'LIST' }],
    }),

    getStatusWorkflow: builder.query<StatusWorkflowDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await statusWorkflowsGetStatusWorkflow(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Tagged by both the resolved GUID id and the original arg (key). The page
      // fetches by key while every mutation invalidates by id — without the id
      // tag those would never refetch.
      providesTags: (result, error, arg) => [
        ...(result ? [{ type: QueryTags.StatusWorkflow, id: result.id }] : []),
        { type: QueryTags.StatusWorkflow, id: arg },
      ],
    }),

    getWorkflowOwnerTypes: builder.query<WorkflowOwnerTypeDto[], void>({
      queryFn: async () => {
        try {
          const data = await statusWorkflowsGetOwnerTypes()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    createStatusWorkflow: builder.mutation<string, CreateStatusWorkflowRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await statusWorkflowsCreate(request)
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: () => [{ type: QueryTags.StatusWorkflow, id: 'LIST' }],
      },
    ),

    updateStatusWorkflow: builder.mutation<
      void,
      { id: string; request: UpdateStatusWorkflowRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await statusWorkflowsUpdate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id: arg.id },
        { type: QueryTags.ActivityLog, id: arg.id },
      ],
    }),

    cloneStatusWorkflow: builder.mutation<
      string,
      { id: string; request: CloneStatusWorkflowRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await statusWorkflowsClone(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.StatusWorkflow, id: 'LIST' }],
    }),

    publishStatusWorkflow: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await statusWorkflowsPublish(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id },
        { type: QueryTags.ActivityLog, id },
      ],
    }),

    archiveStatusWorkflow: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await statusWorkflowsArchive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id },
        { type: QueryTags.ActivityLog, id },
      ],
    }),

    addWorkflowStatus: builder.mutation<
      string,
      { workflowId: string; request: AddWorkflowStatusRequest }
    >({
      queryFn: async ({ workflowId, request }) => {
        try {
          const data = await statusWorkflowsAddStatus(workflowId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Untagged: a status change alters the parent's publishability too.
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    renameWorkflowStatus: builder.mutation<
      void,
      {
        workflowId: string
        statusId: string
        request: RenameWorkflowStatusRequest
      }
    >({
      queryFn: async ({ workflowId, statusId, request }) => {
        try {
          const data = await statusWorkflowsRenameStatus(
            workflowId,
            statusId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    describeWorkflowStatus: builder.mutation<
      void,
      {
        workflowId: string
        statusId: string
        request: DescribeWorkflowStatusRequest
      }
    >({
      queryFn: async ({ workflowId, statusId, request }) => {
        try {
          const data = await statusWorkflowsDescribeStatus(
            workflowId,
            statusId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    reclassifyWorkflowStatus: builder.mutation<
      void,
      {
        workflowId: string
        statusId: string
        request: ReclassifyWorkflowStatusRequest
      }
    >({
      queryFn: async ({ workflowId, statusId, request }) => {
        try {
          const data = await statusWorkflowsReclassifyStatus(
            workflowId,
            statusId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    removeWorkflowStatus: builder.mutation<
      void,
      { workflowId: string; statusId: string }
    >({
      queryFn: async ({ workflowId, statusId }) => {
        try {
          const data = await statusWorkflowsRemoveStatus(workflowId, statusId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    reorderWorkflowStatuses: builder.mutation<
      void,
      { workflowId: string; request: ReorderWorkflowStatusesRequest }
    >({
      queryFn: async ({ workflowId, request }) => {
        try {
          const data = await statusWorkflowsReorderStatuses(workflowId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    setWorkflowTransitionMode: builder.mutation<
      void,
      { workflowId: string; request: SetWorkflowTransitionModeRequest }
    >({
      queryFn: async ({ workflowId, request }) => {
        try {
          const data = await statusWorkflowsSetTransitionMode(
            workflowId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow, id: workflowId },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    setWorkflowTransitions: builder.mutation<
      void,
      { workflowId: string; request: SetWorkflowTransitionsRequest }
    >({
      queryFn: async ({ workflowId, request }) => {
        try {
          const data = await statusWorkflowsSetTransitions(workflowId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { workflowId }) => [
        { type: QueryTags.StatusWorkflow, id: workflowId },
        { type: QueryTags.ActivityLog, id: workflowId },
      ],
    }),

    getWorkflowAssignments: builder.query<
      WorkflowAssignmentDto[],
      string | undefined
    >({
      queryFn: async (ownerType) => {
        try {
          const data = await workflowAssignmentsGetWorkflowAssignments({
            ownerType: ownerType,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.WorkflowAssignment, id: 'LIST' }],
    }),

    previewStatusRemap: builder.query<
      StatusRemapPreviewDto,
      { assignmentId: string; targetWorkflowId: string }
    >({
      queryFn: async ({ assignmentId, targetWorkflowId }) => {
        try {
          const data = await workflowAssignmentsPreviewRemap(assignmentId, {
            targetWorkflowId: targetWorkflowId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    reassignWorkflow: builder.mutation<
      number,
      { assignmentId: string; request: ReassignWorkflowRequest }
    >({
      queryFn: async ({ assignmentId, request }) => {
        try {
          const data = await workflowAssignmentsReassign(assignmentId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Every record of the owner type just changed status, so caches holding
      // those records are stale too — their histories, which gained the switch,
      // and the workflows they are drawn on, which carry the history tag. An
      // intake desk following the default names the workflow it follows.
      invalidatesTags: () => [
        { type: QueryTags.WorkflowAssignment, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.Product },
        { type: QueryTags.StatusHistory },
        { type: QueryTags.IntakeDesk },
      ],
    }),

    getStatusWorkflowActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await statusWorkflowsGetActivities(String(idOrKey), {
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
  useGetStatusWorkflowsQuery,
  useGetStatusWorkflowQuery,
  useGetWorkflowOwnerTypesQuery,
  useCreateStatusWorkflowMutation,
  useUpdateStatusWorkflowMutation,
  useCloneStatusWorkflowMutation,
  usePublishStatusWorkflowMutation,
  useArchiveStatusWorkflowMutation,
  useAddWorkflowStatusMutation,
  useRenameWorkflowStatusMutation,
  useDescribeWorkflowStatusMutation,
  useReclassifyWorkflowStatusMutation,
  useRemoveWorkflowStatusMutation,
  useReorderWorkflowStatusesMutation,
  useSetWorkflowTransitionModeMutation,
  useSetWorkflowTransitionsMutation,
  useGetWorkflowAssignmentsQuery,
  usePreviewStatusRemapQuery,
  useReassignWorkflowMutation,
  useGetStatusWorkflowActivitiesQuery,
  useLazyGetStatusWorkflowActivitiesQuery,
} = statusWorkflowsApi
