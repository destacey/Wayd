import {
  getIntakeChannelsClient,
  getIntakeDesksClient,
  getIntakeEmployeeOptionsClient,
  getIntakeRequestsClient,
  getIntakeRequestTagCategoriesClient,
} from '@/src/services/clients'
import {
  AddIntakeRequestTagRequest,
  ChangeIntakeRequestStatusRequest,
  CreateIntakeRequestTagCategoryRequest,
  IntakeRequestTagCategoryDto,
  ReorderIntakeRequestTagCategoriesRequest,
  UpdateIntakeRequestTagCategoryRequest,
  UpdateIntakeRequestTagRequest,
  CreateIntakeChannelRequest,
  CreateIntakeDeskRequest,
  CreateIntakeRequestRequest,
  IntakeDeskWorkflowDto,
  IntakeRequestArrivalStatusesDto,
  IntakeRequestStatusOptionDto,
  IntakeWorkflowChoiceDto,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  WithdrawIntakeRequestRequest,
  EmployeeNavigationDto,
  IntakeChannelDto,
  IntakeDeskDto,
  IntakeDeskRoleDto,
  IntakeDeskVisibilityDto,
  IntakeRequestDetailsDto,
  IntakeRequestSummaryDto,
  NeedByBasisDto,
  NeedByBasisFamilyDto,
  ObjectIdAndKey,
  PagedResponseOfActivityLogDto,
  ReorderIntakeChannelsRequest,
  RerouteIntakeRequestRequest,
  UpdateIntakeChannelRequest,
  UpdateIntakeDeskRequest,
  UpdateIntakeRequestRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { ActivityLogQueryArg } from '@/src/components/common/activities'

export interface IntakeRequestListArgs {
  deskId?: string
  /** Only requests the caller requested or submitted. */
  mine?: boolean
  /** Only requests still open: in the Proposed or Active status category. */
  openOnly?: boolean
}

export interface IntakeRequestParticipantArgs {
  requestId: string
  employeeId: string
}

/**
 * A request's details are fetched by the key in the URL, and its mutations know only its id, so every
 * entry carries both tags. The activity log is keyed by id, which is what the request page passes.
 */
const requestTags = (requestId: string) => [
  { type: QueryTags.IntakeRequest, id: requestId },
  { type: QueryTags.ActivityLog, id: requestId },
]

/**
 * What a status move changes: the request, its activity and status history, every list it appears in, and
 * the waiting counts on its desk.
 */
const statusMoveTags = (requestId: string) => [
  ...requestTags(requestId),
  { type: QueryTags.StatusHistory, id: requestId },
  listTag,
  QueryTags.IntakeDesk,
]

const listTag = { type: QueryTags.IntakeRequest, id: 'LIST' } as const

const deskListTag = { type: QueryTags.IntakeDesk, id: 'LIST' } as const

const channelListTag = { type: QueryTags.IntakeChannel, id: 'LIST' } as const

const tagCategoryListTag = {
  type: QueryTags.IntakeRequestTagCategory,
  id: 'LIST',
} as const

export interface IntakeRequestTagArgs {
  requestId: string
  tagId: string
}

export interface IntakeRequestTagCategoryTagArgs {
  categoryId: string
  tagId: string
}

export interface ChangeIntakeRequestAssigneeArgs {
  requestId: string
  /** The desk member to assign, or undefined to unassign. */
  assigneeId?: string
}

export interface RerouteIntakeRequestArgs extends RerouteIntakeRequestRequest {
  requestId: string
}

export interface ChangeIntakeRequestStatusArgs extends ChangeIntakeRequestStatusRequest {
  requestId: string
}

export interface WithdrawIntakeRequestArgs extends WithdrawIntakeRequestRequest {
  requestId: string
}

/**
 * Rejects with what the generated client threw, unchanged, so pages and forms read it with `toApiError`:
 * a documented response arrives as its problem details and an undocumented one as an `ApiException`.
 */
const toError = (error: unknown) => {
  console.error('API Error:', error)
  return { error }
}

export const demandManagementApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getIntakeRequests: builder.query<
      IntakeRequestSummaryDto[],
      IntakeRequestListArgs | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeRequestsClient().getIntakeRequests(
            args?.deskId,
            args?.mine,
            args?.openOnly,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result = []) => [
        listTag,
        ...result.map(({ id }) => ({ type: QueryTags.IntakeRequest, id })),
      ],
    }),

    getIntakeRequest: builder.query<IntakeRequestDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getIntakeRequestsClient().getIntakeRequest(idOrKey)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result, _error, idOrKey) => [
        ...(result
          ? [{ type: QueryTags.IntakeRequest, id: result.summary.id }]
          : []),
        { type: QueryTags.IntakeRequest, id: idOrKey },
      ],
    }),

    // Takes the shared activity-log argument so the request page can drive it with useActivityLog. The
    // request page passes the id, which is what mutations invalidate by.
    getIntakeRequestActivities: builder.query<
      PagedResponseOfActivityLogDto,
      ActivityLogQueryArg
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await getIntakeRequestsClient().getActivities(
            String(idOrKey),
            page,
            pageSize,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (_result, _error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),

    createIntakeRequest: builder.mutation<
      ObjectIdAndKey,
      CreateIntakeRequestRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getIntakeRequestsClient().create(request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // A new request changes its desk's waiting count and whether the desk can be deleted.
      invalidatesTags: [listTag, QueryTags.IntakeDesk],
    }),

    updateIntakeRequest: builder.mutation<void, UpdateIntakeRequestRequest>({
      queryFn: async (request) => {
        try {
          const data = await getIntakeRequestsClient().update(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { id }) => [
        ...requestTags(id),
        listTag,
      ],
    }),

    addIntakeRequestParticipant: builder.mutation<
      void,
      IntakeRequestParticipantArgs
    >({
      queryFn: async ({ requestId, employeeId }) => {
        try {
          const data = await getIntakeRequestsClient().addParticipant(
            requestId,
            { employeeId },
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) =>
        requestTags(requestId),
    }),

    // The assignee shows in every list the request appears in, so lists refetch too.
    changeIntakeRequestAssignee: builder.mutation<
      void,
      ChangeIntakeRequestAssigneeArgs
    >({
      queryFn: async ({ requestId, assigneeId }) => {
        try {
          const data = await getIntakeRequestsClient().changeAssignee(
            requestId,
            { assigneeId },
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) => [
        ...requestTags(requestId),
        listTag,
      ],
    }),

    // A move changes the desk on the request and the waiting counts on both desks.
    rerouteIntakeRequest: builder.mutation<void, RerouteIntakeRequestArgs>({
      queryFn: async ({ requestId, ...request }) => {
        try {
          const data = await getIntakeRequestsClient().reroute(
            requestId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // A move can change the request's status too.
      invalidatesTags: (_result, _error, { requestId }) =>
        statusMoveTags(requestId),
    }),

    // The statuses the request can arrive in on a desk, which depend on the status it holds.
    getIntakeRequestArrivalStatuses: builder.query<
      IntakeRequestArrivalStatusesDto,
      { requestId: string; deskId: string }
    >({
      queryFn: async ({ requestId, deskId }) => {
        try {
          const data = await getIntakeRequestsClient().getArrivalStatuses(
            requestId,
            deskId,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // Tagged with the destination desk too: its workflow decides the statuses.
      providesTags: (_result, _error, { requestId, deskId }) => [
        { type: QueryTags.IntakeRequest, id: requestId },
        { type: QueryTags.IntakeDesk, id: deskId },
      ],
    }),

    // Fetched by id: the options depend on the status the request holds, which a move invalidates by id.
    getIntakeRequestStatusOptions: builder.query<
      IntakeRequestStatusOptionDto[],
      string
    >({
      queryFn: async (requestId) => {
        try {
          const data =
            await getIntakeRequestsClient().getStatusOptions(requestId)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (_result, _error, requestId) => [
        { type: QueryTags.IntakeRequest, id: requestId },
      ],
    }),

    changeIntakeRequestStatus: builder.mutation<
      void,
      ChangeIntakeRequestStatusArgs
    >({
      queryFn: async ({ requestId, ...request }) => {
        try {
          const data = await getIntakeRequestsClient().changeStatus(
            requestId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) =>
        statusMoveTags(requestId),
    }),

    withdrawIntakeRequest: builder.mutation<void, WithdrawIntakeRequestArgs>({
      queryFn: async ({ requestId, ...request }) => {
        try {
          const data = await getIntakeRequestsClient().withdraw(
            requestId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) =>
        statusMoveTags(requestId),
    }),

    /**
     * Fetched by id, the tag every status move invalidates, so the history never shows what it was before
     * the change.
     */
    getIntakeRequestStatusHistory: builder.query<StatusTransitionDto[], string>(
      {
        queryFn: async (requestId) => {
          try {
            const data =
              await getIntakeRequestsClient().getStatusHistory(requestId)
            return { data }
          } catch (error) {
            return toError(error)
          }
        },
        providesTags: (_result, _error, requestId) => [
          { type: QueryTags.StatusHistory, id: requestId },
        ],
      },
    ),

    // Tagged with the history too: a move to a desk on another workflow switches the request onto it.
    getIntakeRequestStatusWorkflow: builder.query<
      StatusWorkflowDetailsDto,
      string
    >({
      queryFn: async (requestId) => {
        try {
          const data =
            await getIntakeRequestsClient().getStatusWorkflow(requestId)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result, _error, requestId) => [
        ...(result ? [{ type: QueryTags.StatusWorkflow, id: result.id }] : []),
        { type: QueryTags.StatusHistory, id: requestId },
      ],
    }),

    removeIntakeRequestParticipant: builder.mutation<
      void,
      IntakeRequestParticipantArgs
    >({
      queryFn: async ({ requestId, employeeId }) => {
        try {
          const data = await getIntakeRequestsClient().removeParticipant(
            requestId,
            employeeId,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) =>
        requestTags(requestId),
    }),

    getNeedByBases: builder.query<NeedByBasisDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeRequestsClient().getNeedByBases()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'NEED-BY-BASES' }],
    }),

    getNeedByBasisFamilies: builder.query<NeedByBasisFamilyDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeRequestsClient().getNeedByBasisFamilies()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'NEED-BY-FAMILIES' }],
    }),

    getIntakeDesks: builder.query<
      IntakeDeskDto[],
      { submittableOnly?: boolean } | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeDesksClient().getIntakeDesks(
            args?.submittableOnly,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result = []) => [
        deskListTag,
        ...result.map(({ id }) => ({ type: QueryTags.IntakeDesk, id })),
      ],
    }),

    // Fetched by the key in the URL; mutations know only the id, so the entry carries both tags.
    getIntakeDesk: builder.query<IntakeDeskDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getIntakeDesksClient().getIntakeDesk(idOrKey)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result, _error, idOrKey) => [
        ...(result ? [{ type: QueryTags.IntakeDesk, id: result.id }] : []),
        { type: QueryTags.IntakeDesk, id: idOrKey },
      ],
    }),

    updateIntakeDesk: builder.mutation<void, UpdateIntakeDeskRequest>({
      queryFn: async (request) => {
        try {
          const data = await getIntakeDesksClient().update(request.id, request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // Removing a member unassigns them from the desk's requests, and a rename shows on every request
      // on the desk, so every cached request refreshes too. The channels and tag axes it offers may have changed.
      invalidatesTags: (_result, _error, { id }) => [
        { type: QueryTags.IntakeDesk, id },
        deskListTag,
        QueryTags.IntakeRequest,
        channelListTag,
        tagCategoryListTag,
      ],
    }),

    deleteIntakeDesk: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getIntakeDesksClient().delete(id)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [deskListTag],
    }),

    // The workflow of each desk, for the Requests board, which draws one only when they agree. Tagged by
    // workflow rather than by desk: its columns change when a workflow is edited or a desk is pointed at
    // another, never when a request moves, and status moves invalidate every desk.
    getIntakeDeskWorkflows: builder.query<IntakeDeskWorkflowDto[], string[]>({
      queryFn: async (deskIds) => {
        try {
          const client = getIntakeDesksClient()
          const data = await Promise.all(
            deskIds.map((id) => client.getWorkflow(id)),
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result = []) => [
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
        { type: QueryTags.WorkflowAssignment, id: 'LIST' },
        ...result.map((w) => ({
          type: QueryTags.StatusWorkflow,
          id: w.workflow.id,
        })),
      ],
    }),

    // Fetched by id, which the change invalidates; tagged with the desk too, since a request arriving on it
    // stops the workflow changing, and with the workflow, whose statuses it lists.
    getIntakeDeskWorkflow: builder.query<IntakeDeskWorkflowDto, string>({
      queryFn: async (deskId) => {
        try {
          const data = await getIntakeDesksClient().getWorkflow(deskId)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result, _error, deskId) => [
        { type: QueryTags.IntakeDesk, id: deskId },
        ...(result
          ? [{ type: QueryTags.StatusWorkflow, id: result.workflow.id }]
          : []),
      ],
    }),

    getIntakeWorkflowChoices: builder.query<IntakeWorkflowChoiceDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeDesksClient().getWorkflowChoices()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [QueryTags.StatusWorkflow],
    }),

    changeIntakeDeskWorkflow: builder.mutation<
      void,
      { deskId: string; workflowId: string }
    >({
      queryFn: async ({ deskId, workflowId }) => {
        try {
          const data = await getIntakeDesksClient().changeWorkflow(deskId, {
            workflowId,
          })
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // Settings → Status Workflows lists the assignments and marks the workflows in use, and a request's
      // arrival statuses on this desk come from its workflow.
      invalidatesTags: (_result, _error, { deskId }) => [
        { type: QueryTags.IntakeDesk, id: deskId },
        { type: QueryTags.WorkflowAssignment, id: 'LIST' },
        { type: QueryTags.StatusWorkflow, id: 'LIST' },
      ],
    }),

    getIntakeDeskRoles: builder.query<IntakeDeskRoleDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeDesksClient().getRoles()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'DESK-ROLES' }],
    }),

    createIntakeDesk: builder.mutation<ObjectIdAndKey, CreateIntakeDeskRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await getIntakeDesksClient().create(request)
            return { data }
          } catch (error) {
            return toError(error)
          }
        },
        invalidatesTags: [deskListTag],
      },
    ),

    getIntakeDeskVisibilities: builder.query<IntakeDeskVisibilityDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeDesksClient().getVisibilities()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'VISIBILITIES' }],
    }),

    // With a desk, only the active channels it offers; otherwise the company list.
    getIntakeChannels: builder.query<
      IntakeChannelDto[],
      { includeArchived?: boolean; deskId?: string } | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeChannelsClient().getIntakeChannels(
            args?.includeArchived,
            args?.deskId,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [channelListTag],
    }),

    createIntakeChannel: builder.mutation<string, CreateIntakeChannelRequest>({
      queryFn: async (request) => {
        try {
          const data = await getIntakeChannelsClient().create(request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [channelListTag],
    }),

    // A rename shows on every request carrying the channel.
    updateIntakeChannel: builder.mutation<void, UpdateIntakeChannelRequest>({
      queryFn: async (request) => {
        try {
          const data = await getIntakeChannelsClient().update(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [channelListTag, QueryTags.IntakeRequest],
    }),

    archiveIntakeChannel: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getIntakeChannelsClient().archive(id)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [channelListTag],
    }),

    restoreIntakeChannel: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getIntakeChannelsClient().restore(id)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [channelListTag],
    }),

    reorderIntakeChannels: builder.mutation<void, ReorderIntakeChannelsRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await getIntakeChannelsClient().reorder(request)
            return { data }
          } catch (error) {
            return toError(error)
          }
        },
        invalidatesTags: [channelListTag],
      },
    ),

    // Every signed-in user reads these: whoever sees a request sees its tags and can filter on them. With a
    // desk, only the active axes it offers its agents.
    getIntakeRequestTagCategories: builder.query<
      IntakeRequestTagCategoryDto[],
      { includeInactive?: boolean; deskId?: string } | void
    >({
      queryFn: async (args) => {
        try {
          const data =
            await getIntakeRequestTagCategoriesClient().getIntakeRequestTagCategories(
              args?.includeInactive,
              args?.deskId,
            )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [tagCategoryListTag],
    }),

    createIntakeRequestTagCategory: builder.mutation<
      string,
      CreateIntakeRequestTagCategoryRequest
    >({
      queryFn: async (request) => {
        try {
          const data =
            await getIntakeRequestTagCategoriesClient().create(request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag],
    }),

    // A rename shows on every request tagged along the axis.
    updateIntakeRequestTagCategory: builder.mutation<
      void,
      UpdateIntakeRequestTagCategoryRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getIntakeRequestTagCategoriesClient().update(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag, QueryTags.IntakeRequest],
    }),

    setIntakeRequestTagCategoryActive: builder.mutation<
      void,
      { id: string; isActive: boolean }
    >({
      queryFn: async ({ id, isActive }) => {
        try {
          const data = await getIntakeRequestTagCategoriesClient().setActive(
            id,
            { isActive },
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag],
    }),

    // Requests list their tags in axis order.
    reorderIntakeRequestTagCategories: builder.mutation<
      void,
      ReorderIntakeRequestTagCategoriesRequest
    >({
      queryFn: async (request) => {
        try {
          const data =
            await getIntakeRequestTagCategoriesClient().reorder(request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag, QueryTags.IntakeRequest],
    }),

    deleteIntakeRequestTagCategory: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getIntakeRequestTagCategoriesClient().delete(id)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag],
    }),

    addIntakeRequestTag: builder.mutation<
      string,
      AddIntakeRequestTagRequest & { categoryId: string }
    >({
      queryFn: async ({ categoryId, ...request }) => {
        try {
          const data = await getIntakeRequestTagCategoriesClient().addTag(
            categoryId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag],
    }),

    // A rename shows on every request carrying the tag.
    updateIntakeRequestTag: builder.mutation<
      void,
      UpdateIntakeRequestTagRequest & IntakeRequestTagCategoryTagArgs
    >({
      queryFn: async ({ categoryId, tagId, ...request }) => {
        try {
          const data = await getIntakeRequestTagCategoriesClient().updateTag(
            categoryId,
            tagId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag, QueryTags.IntakeRequest],
    }),

    setIntakeRequestTagActive: builder.mutation<
      void,
      IntakeRequestTagCategoryTagArgs & { isActive: boolean }
    >({
      queryFn: async ({ categoryId, tagId, isActive }) => {
        try {
          const data = await getIntakeRequestTagCategoriesClient().setTagActive(
            categoryId,
            tagId,
            { isActive },
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag],
    }),

    deleteIntakeRequestTag: builder.mutation<
      void,
      IntakeRequestTagCategoryTagArgs
    >({
      queryFn: async ({ categoryId, tagId }) => {
        try {
          const data = await getIntakeRequestTagCategoriesClient().deleteTag(
            categoryId,
            tagId,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag],
    }),

    // Changes the request, its activity, every list it appears in, and the tag counts.
    tagIntakeRequest: builder.mutation<void, IntakeRequestTagArgs>({
      queryFn: async ({ requestId, tagId }) => {
        try {
          const data = await getIntakeRequestsClient().tag(requestId, tagId)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) => [
        ...requestTags(requestId),
        listTag,
        tagCategoryListTag,
      ],
    }),

    untagIntakeRequest: builder.mutation<void, IntakeRequestTagArgs>({
      queryFn: async ({ requestId, tagId }) => {
        try {
          const data = await getIntakeRequestsClient().untag(requestId, tagId)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) => [
        ...requestTags(requestId),
        listTag,
        tagCategoryListTag,
      ],
    }),

    getIntakeEmployeeOptions: builder.query<EmployeeNavigationDto[], void>({
      queryFn: async () => {
        try {
          const data =
            await getIntakeEmployeeOptionsClient().getEmployeeOptions()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeEmployeeOption, id: 'LIST' }],
    }),
  }),
})

export const {
  useGetIntakeRequestsQuery,
  useGetIntakeRequestQuery,
  useGetIntakeRequestActivitiesQuery,
  useLazyGetIntakeRequestActivitiesQuery,
  useCreateIntakeRequestMutation,
  useUpdateIntakeRequestMutation,
  useAddIntakeRequestParticipantMutation,
  useRemoveIntakeRequestParticipantMutation,
  useGetNeedByBasesQuery,
  useGetNeedByBasisFamiliesQuery,
  useGetIntakeDesksQuery,
  useGetIntakeDeskQuery,
  useGetIntakeDeskVisibilitiesQuery,
  useGetIntakeDeskRolesQuery,
  useGetIntakeDeskWorkflowQuery,
  useGetIntakeWorkflowChoicesQuery,
  useChangeIntakeDeskWorkflowMutation,
  useCreateIntakeDeskMutation,
  useUpdateIntakeDeskMutation,
  useDeleteIntakeDeskMutation,
  useRerouteIntakeRequestMutation,
  useGetIntakeDeskWorkflowsQuery,
  useChangeIntakeRequestAssigneeMutation,
  useGetIntakeRequestArrivalStatusesQuery,
  useGetIntakeRequestStatusOptionsQuery,
  useChangeIntakeRequestStatusMutation,
  useWithdrawIntakeRequestMutation,
  useGetIntakeRequestStatusHistoryQuery,
  useGetIntakeRequestStatusWorkflowQuery,
  useGetIntakeChannelsQuery,
  useCreateIntakeChannelMutation,
  useUpdateIntakeChannelMutation,
  useArchiveIntakeChannelMutation,
  useRestoreIntakeChannelMutation,
  useReorderIntakeChannelsMutation,
  useGetIntakeRequestTagCategoriesQuery,
  useCreateIntakeRequestTagCategoryMutation,
  useUpdateIntakeRequestTagCategoryMutation,
  useSetIntakeRequestTagCategoryActiveMutation,
  useReorderIntakeRequestTagCategoriesMutation,
  useDeleteIntakeRequestTagCategoryMutation,
  useAddIntakeRequestTagMutation,
  useUpdateIntakeRequestTagMutation,
  useSetIntakeRequestTagActiveMutation,
  useDeleteIntakeRequestTagMutation,
  useTagIntakeRequestMutation,
  useUntagIntakeRequestMutation,
  useGetIntakeEmployeeOptionsQuery,
} = demandManagementApi
