import {
  getIntakeChannelsClient,
  getIntakeDesksClient,
  getIntakeEmployeeOptionsClient,
  getIntakeRequestsClient,
  getIntakeRequestTagCategoriesClient,
  getIntakeRequestTagsClient,
} from '@/src/services/clients'
import {
  discussionTag,
  DiscussionChannelTypes,
} from '../common/discussions-api'
import {
  AddIntakeRequestOutcomeRequest,
  AddIntakeRequestTagRequest,
  ProjectIntakeRequestDto,
  ChangeIntakeRequestStatusRequest,
  CreateIntakeRequestTagCategoryRequest,
  IntakeRequestTagCategoryDto,
  IntakeRequestTagListItemDto,
  UpdateFreeIntakeRequestTagRequest,
  ReorderIntakeRequestTagCategoriesRequest,
  UpdateIntakeRequestTagCategoryRequest,
  UpdateIntakeRequestTagRequest,
  CreateIntakeChannelRequest,
  CreateIntakeDeskRequest,
  CreateIntakeRequestRequest,
  IntakeDeskWorkflowDto,
  IntakeRequestCurrentScoreDto,
  IntakeRequestScoringDto,
  IntakeScoringModelChoiceDto,
  ScoreIntakeRequestRequest,
  ScoringModelEvaluationDto,
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

export interface AddIntakeRequestOutcomeArgs {
  requestId: string
  request: AddIntakeRequestOutcomeRequest
}

export interface RemoveIntakeRequestOutcomeArgs {
  requestId: string
  /** The project the outcome points at. */
  targetId: string
}

/** A project's list of the requests it came from or absorbed. */
const projectRequestsTag = (projectId: string) =>
  ({ type: QueryTags.IntakeRequest, id: `PROJECT-${projectId}` }) as const

/**
 * A request's details are fetched by the key in the URL, and its mutations know only its id, so every
 * entry carries both tags. The activity log is keyed by id, which is what the request page passes.
 */
const requestTags = (requestId: string) => [
  { type: QueryTags.IntakeRequest, id: requestId },
  { type: QueryTags.ActivityLog, id: requestId },
  // The request's conversation shows its changes between the messages.
  discussionTag({
    channelType: DiscussionChannelTypes.IntakeRequest,
    ownerId: requestId,
  }),
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
  // A move in or out of an open status, or to another desk, changes which requests rank together.
  QueryTags.IntakeRequestScore,
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

export interface TagIntakeRequestByNameArgs {
  requestId: string
  /** The typed name; the server applies the matching tag or a free tag of that name. */
  name: string
}

export interface SearchIntakeRequestTagsArgs {
  search?: string
  /** True for free tags only, false for tags on axes only, omitted for both. */
  isFree?: boolean
  includeInactive?: boolean
}

export interface MergeIntakeRequestTagArgs {
  /** The free tag to merge away. */
  id: string
  /** The tag its requests move to. */
  targetTagId: string
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

export interface ScoreIntakeRequestArgs extends ScoreIntakeRequestRequest {
  requestId: string
}

export interface ChangeIntakeDeskScoringModelArgs {
  deskId: string
  /** An active model, or undefined to stop scoring the desk's requests. */
  scoringModelId?: string
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

    // Answered beside the list rather than on it, since a score is substance and a summary is not.
    getIntakeRequestScores: builder.query<
      IntakeRequestCurrentScoreDto[],
      IntakeRequestListArgs | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeRequestsClient().getIntakeRequestScores(
            args?.deskId,
            args?.mine,
            args?.openOnly,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeRequestScore, id: 'LIST' }],
    }),

    getIntakeRequestScoring: builder.query<IntakeRequestScoringDto, string>({
      queryFn: async (requestId) => {
        try {
          const data = await getIntakeRequestsClient().getScoring(requestId)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (_result, _error, requestId) => [
        { type: QueryTags.IntakeRequestScore, id: requestId },
      ],
    }),

    // A new score moves the request among the others on its desk, so every request's rank is stale.
    scoreIntakeRequest: builder.mutation<string, ScoreIntakeRequestArgs>({
      queryFn: async ({ requestId, ...request }) => {
        try {
          const data = await getIntakeRequestsClient().score(requestId, request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) => [
        { type: QueryTags.ActivityLog, id: requestId },
        QueryTags.IntakeRequestScore,
      ],
    }),

    // A mutation only because the ratings travel in a body: it records nothing, so it invalidates nothing.
    previewIntakeRequestScore: builder.mutation<
      ScoringModelEvaluationDto,
      ScoreIntakeRequestArgs
    >({
      queryFn: async ({ requestId, ...request }) => {
        try {
          const data = await getIntakeRequestsClient().previewScore(
            requestId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
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

    // Outcomes change whether finishing the request needs a reason; the request's tag refetches its status
    // options too.
    addIntakeRequestOutcome: builder.mutation<
      void,
      AddIntakeRequestOutcomeArgs
    >({
      queryFn: async ({ requestId, request }) => {
        try {
          const data = await getIntakeRequestsClient().addOutcome(
            requestId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId, request }) => [
        ...requestTags(requestId),
        projectRequestsTag(request.projectId),
      ],
    }),

    removeIntakeRequestOutcome: builder.mutation<
      void,
      RemoveIntakeRequestOutcomeArgs
    >({
      queryFn: async ({ requestId, targetId }) => {
        try {
          const data = await getIntakeRequestsClient().removeOutcome(
            requestId,
            targetId,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId, targetId }) => [
        ...requestTags(requestId),
        projectRequestsTag(targetId),
      ],
    }),

    getProjectIntakeRequests: builder.query<ProjectIntakeRequestDto[], string>({
      queryFn: async (projectId) => {
        try {
          const data =
            await getIntakeRequestsClient().getProjectIntakeRequests(projectId)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // Tagged with each request too, so a status change or rename on one refreshes the project's list.
      providesTags: (result = [], _error, projectId) => [
        projectRequestsTag(projectId),
        ...result.map(({ request }) => ({
          type: QueryTags.IntakeRequest,
          id: request.id,
        })),
      ],
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
        // Members and visibility decide whose scores a reader may read, and so which ones rank.
        QueryTags.IntakeRequestScore,
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

    getIntakeScoringModelChoices: builder.query<
      IntakeScoringModelChoiceDto[],
      void
    >({
      queryFn: async () => {
        try {
          const data = await getIntakeDesksClient().getScoringModelChoices()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // Settings activates and archives models under this tag, which is what changes the choices.
      providesTags: [QueryTags.ScoringModel],
    }),

    // Which model a desk scores with decides what its requests are rated on and which scores rank together.
    changeIntakeDeskScoringModel: builder.mutation<
      void,
      ChangeIntakeDeskScoringModelArgs
    >({
      queryFn: async ({ deskId, scoringModelId }) => {
        try {
          const data = await getIntakeDesksClient().changeScoringModel(deskId, {
            scoringModelId,
          })
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { deskId }) => [
        { type: QueryTags.IntakeDesk, id: deskId },
        deskListTag,
        QueryTags.IntakeRequestScore,
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

    // Returns the id of the tag applied, which may be a free tag created for the name.
    tagIntakeRequestByName: builder.mutation<
      string,
      TagIntakeRequestByNameArgs
    >({
      queryFn: async ({ requestId, name }) => {
        try {
          const data = await getIntakeRequestsClient().tagByName(requestId, {
            name,
          })
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

    // Tagged with the axes' list: every change that moves tag counts already
    // invalidates it, and free tags come and go with those same changes.
    searchIntakeRequestTags: builder.query<
      IntakeRequestTagListItemDto[],
      SearchIntakeRequestTagsArgs | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeRequestTagsClient().search(
            args?.search,
            args?.isFree,
            args?.includeInactive,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [tagCategoryListTag],
    }),

    // A rename shows on every request carrying the tag.
    updateFreeIntakeRequestTag: builder.mutation<
      void,
      UpdateFreeIntakeRequestTagRequest & { id: string }
    >({
      queryFn: async ({ id, ...request }) => {
        try {
          const data = await getIntakeRequestTagsClient().updateFreeTag(
            id,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [tagCategoryListTag, QueryTags.IntakeRequest],
    }),

    // Retags requests on every desk, so every request and its activity may change.
    mergeIntakeRequestTag: builder.mutation<void, MergeIntakeRequestTagArgs>({
      queryFn: async ({ id, targetTagId }) => {
        try {
          const data = await getIntakeRequestTagsClient().merge(id, {
            targetTagId,
          })
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [
        tagCategoryListTag,
        QueryTags.IntakeRequest,
        QueryTags.ActivityLog,
      ],
    }),

    // Takes the tag off every request carrying it.
    deleteFreeIntakeRequestTag: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getIntakeRequestTagsClient().deleteFreeTag(id)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [
        tagCategoryListTag,
        QueryTags.IntakeRequest,
        QueryTags.ActivityLog,
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
  useAddIntakeRequestOutcomeMutation,
  useRemoveIntakeRequestOutcomeMutation,
  useGetProjectIntakeRequestsQuery,
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
  useTagIntakeRequestByNameMutation,
  useSearchIntakeRequestTagsQuery,
  useUpdateFreeIntakeRequestTagMutation,
  useMergeIntakeRequestTagMutation,
  useDeleteFreeIntakeRequestTagMutation,
  useGetIntakeEmployeeOptionsQuery,
  useGetIntakeRequestScoresQuery,
  useGetIntakeRequestScoringQuery,
  useScoreIntakeRequestMutation,
  usePreviewIntakeRequestScoreMutation,
  useGetIntakeScoringModelChoicesQuery,
  useChangeIntakeDeskScoringModelMutation,
} = demandManagementApi
