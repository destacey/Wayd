import {
  AddWorkflowStatusRequest,
  CloneStatusWorkflowRequest,
  CreateStatusWorkflowRequest,
  ReassignWorkflowRequest,
  ReclassifyWorkflowStatusRequest,
  RenameWorkflowStatusRequest,
  ReorderWorkflowStatusesRequest,
  StatusRemapPreviewDto,
  StatusWorkflowDetailsDto,
  StatusWorkflowListDto,
  StatusWorkflowState,
  UpdateStatusWorkflowRequest,
  WorkflowAssignmentDto,
  WorkflowOwnerTypeDto,
} from '@/src/services/wayd-api'
import {
  getStatusWorkflowsClient,
  getWorkflowAssignmentsClient,
} from '@/src/services/clients'
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
          const data = await getStatusWorkflowsClient().getStatusWorkflows(
            args?.ownerType,
            args?.state,
          )
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
          const data = await getStatusWorkflowsClient().getStatusWorkflow(idOrKey)
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
          const data = await getStatusWorkflowsClient().getOwnerTypes()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    createStatusWorkflow: builder.mutation<string, CreateStatusWorkflowRequest>({
      queryFn: async (request) => {
        try {
          const data = await getStatusWorkflowsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.StatusWorkflow, id: 'LIST' }],
    }),

    updateStatusWorkflow: builder.mutation<
      void,
      { id: string; request: UpdateStatusWorkflowRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getStatusWorkflowsClient().update(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id: arg.id },
      ],
    }),

    cloneStatusWorkflow: builder.mutation<
      string,
      { id: string; request: CloneStatusWorkflowRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getStatusWorkflowsClient().clone(id, request)
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
          const data = await getStatusWorkflowsClient().publish(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id },
      ],
    }),

    archiveStatusWorkflow: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getStatusWorkflowsClient().archive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id },
      ],
    }),

    addWorkflowStatus: builder.mutation<
      string,
      { workflowId: string; request: AddWorkflowStatusRequest }
    >({
      queryFn: async ({ workflowId, request }) => {
        try {
          const data = await getStatusWorkflowsClient().addStatus(workflowId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Untagged: a status change alters the parent's publishability too.
      invalidatesTags: () => [{ type: QueryTags.StatusWorkflow }],
    }),

    renameWorkflowStatus: builder.mutation<
      void,
      { workflowId: string; statusId: string; request: RenameWorkflowStatusRequest }
    >({
      queryFn: async ({ workflowId, statusId, request }) => {
        try {
          const data = await getStatusWorkflowsClient().renameStatus(
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
      invalidatesTags: () => [{ type: QueryTags.StatusWorkflow }],
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
          const data = await getStatusWorkflowsClient().reclassifyStatus(
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
      invalidatesTags: () => [{ type: QueryTags.StatusWorkflow }],
    }),

    removeWorkflowStatus: builder.mutation<
      void,
      { workflowId: string; statusId: string }
    >({
      queryFn: async ({ workflowId, statusId }) => {
        try {
          const data = await getStatusWorkflowsClient().removeStatus(
            workflowId,
            statusId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.StatusWorkflow }],
    }),

    reorderWorkflowStatuses: builder.mutation<
      void,
      { workflowId: string; request: ReorderWorkflowStatusesRequest }
    >({
      queryFn: async ({ workflowId, request }) => {
        try {
          const data = await getStatusWorkflowsClient().reorderStatuses(
            workflowId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.StatusWorkflow }],
    }),

    getWorkflowAssignments: builder.query<
      WorkflowAssignmentDto[],
      string | undefined
    >({
      queryFn: async (ownerType) => {
        try {
          const data =
            await getWorkflowAssignmentsClient().getWorkflowAssignments(ownerType)
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
          const data = await getWorkflowAssignmentsClient().previewRemap(
            assignmentId,
            targetWorkflowId,
          )
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
          const data = await getWorkflowAssignmentsClient().reassign(
            assignmentId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Every record of the owner type just changed status, so caches holding
      // those records are stale too.
      invalidatesTags: () => [
        { type: QueryTags.WorkflowAssignment, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.Product },
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
  useReclassifyWorkflowStatusMutation,
  useRemoveWorkflowStatusMutation,
  useReorderWorkflowStatusesMutation,
  useGetWorkflowAssignmentsQuery,
  usePreviewStatusRemapQuery,
  useReassignWorkflowMutation,
} = statusWorkflowsApi
