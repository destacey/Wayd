import type { ThunkDispatch, UnknownAction } from '@reduxjs/toolkit'
import {
  pokerSessionsAddRound,
  pokerSessionsComplete,
  pokerSessionsCreate,
  pokerSessionsDelete,
  pokerSessionsFocusRound,
  pokerSessionsGetInvitedTeamMembers,
  pokerSessionsGetList,
  pokerSessionsGetSession,
  pokerSessionsJoin,
  pokerSessionsPublishRound,
  pokerSessionsPublishRounds,
  pokerSessionsRemoveRound,
  pokerSessionsResetRound,
  pokerSessionsRevealRound,
  pokerSessionsSetConsensus,
  pokerSessionsSetFacilitators,
  pokerSessionsSetInvitees,
  pokerSessionsSetVisibility,
  pokerSessionsSetVoting,
  pokerSessionsSubmitVote,
  pokerSessionsUpdate,
  pokerSessionsUpdateRoundDescription,
  pokerSessionsUpdateRoundLabel,
  pokerSessionsWithdrawVote,
} from '@/src/services/api/poker-sessions'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  AddPokerRoundRequest,
  CreatePokerSessionRequest,
  EmployeeNavigationDto,
  ObjectIdAndKey,
  PokerRoundDto,
  PokerSessionDetailsDto,
  PokerSessionHostKind,
  PokerSessionListDto,
  PokerSessionMemberDto,
  PokerSessionStatus,
  SetConsensusRequest,
  SubmitVoteRequest,
  UpdatePokerRoundDescriptionRequest,
  UpdatePokerRoundLabelRequest,
  Visibility,
} from '@/src/services/api/wayd.schemas'

export interface UpdatePokerSessionRequest {
  name: string
  estimationScaleId: string
}

export const pokerSessionsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPokerSessions: builder.query<
      PokerSessionListDto[],
      PokerSessionStatus | undefined
    >({
      queryFn: async (status) => {
        try {
          const data = await pokerSessionsGetList({ status: status })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.PokerSession, id: 'LIST' }],
    }),

    /** The sessions one host holds, such as an intake desk's, newest first. */
    getHostedPokerSessions: builder.query<
      PokerSessionListDto[],
      { hostKind: PokerSessionHostKind; hostId: string }
    >({
      queryFn: async ({ hostKind, hostId }) => {
        try {
          const data = await pokerSessionsGetList({
            hostKind: hostKind,
            hostId: hostId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.PokerSession, id: 'LIST' }],
    }),

    getPokerSession: builder.query<PokerSessionDetailsDto, string>({
      queryFn: async (idOrKey: string) => {
        try {
          const data = await pokerSessionsGetSession(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        { type: QueryTags.PokerSession, id: result?.key },
        { type: QueryTags.PokerSessionRound, id: result?.key },
      ],
    }),

    createPokerSession: builder.mutation<
      ObjectIdAndKey,
      CreatePokerSessionRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await pokerSessionsCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.PokerSession, id: 'LIST' }],
    }),

    updatePokerSession: builder.mutation<
      void,
      { id: string; key: number; request: UpdatePokerSessionRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await pokerSessionsUpdate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: 'LIST' },
        { type: QueryTags.PokerSession, id: arg.key },
        { type: QueryTags.PokerSessionRound, id: arg.key },
      ],
    }),

    deletePokerSession: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await pokerSessionsDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.PokerSession, id: 'LIST' }],
    }),

    completePokerSession: builder.mutation<void, { id: string; key: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await pokerSessionsComplete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: 'LIST' },
        { type: QueryTags.PokerSession, id: arg.key },
      ],
    }),

    addPokerRound: builder.mutation<
      PokerRoundDto,
      { sessionId: string; sessionKey: number; request: AddPokerRoundRequest }
    >({
      queryFn: async ({ sessionId, request }) => {
        try {
          const data = await pokerSessionsAddRound(sessionId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: arg.sessionKey },
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    removePokerRound: builder.mutation<
      void,
      { sessionId: string; roundId: string; sessionKey: number }
    >({
      queryFn: async ({ sessionId, roundId }) => {
        try {
          const data = await pokerSessionsRemoveRound(sessionId, roundId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: arg.sessionKey },
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    revealPokerRound: builder.mutation<
      void,
      { sessionId: string; roundId: string; sessionKey: number }
    >({
      queryFn: async ({ sessionId, roundId }) => {
        try {
          const data = await pokerSessionsRevealRound(sessionId, roundId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    resetPokerRound: builder.mutation<
      void,
      { sessionId: string; roundId: string; sessionKey: number }
    >({
      queryFn: async ({ sessionId, roundId }) => {
        try {
          const data = await pokerSessionsResetRound(sessionId, roundId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    setPokerRoundConsensus: builder.mutation<
      void,
      {
        sessionId: string
        roundId: string
        sessionKey: number
        request: SetConsensusRequest
      }
    >({
      queryFn: async ({ sessionId, roundId, request }) => {
        try {
          const data = await pokerSessionsSetConsensus(
            sessionId,
            roundId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    /**
     * Publishes one accepted round, or every unpublished accepted round when
     * no round is named, to the records they estimate. Succeeds even when a
     * record's area refuses: each returned round's `publication` says which.
     */
    publishPokerRounds: builder.mutation<
      PokerRoundDto[],
      { sessionId: string; sessionKey: number; roundId?: string }
    >({
      queryFn: async ({ sessionId, roundId }) => {
        try {
          const data = roundId
            ? [await pokerSessionsPublishRound(sessionId, roundId)]
            : await pokerSessionsPublishRounds(sessionId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A published round changes its request's estimate, and with it the
      // desk's unestimated count.
      invalidatesTags: () => [
        QueryTags.PokerSessionRound,
        QueryTags.IntakeRequest,
        QueryTags.IntakeRequestEstimate,
        QueryTags.IntakeDesk,
      ],
    }),

    submitPokerVote: builder.mutation<
      void,
      {
        sessionId: string
        roundId: string
        sessionKey: number
        request: SubmitVoteRequest
      }
    >({
      queryFn: async ({ sessionId, roundId, request }) => {
        try {
          const data = await pokerSessionsSubmitVote(
            sessionId,
            roundId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    withdrawPokerVote: builder.mutation<
      void,
      { sessionId: string; roundId: string; sessionKey: number }
    >({
      queryFn: async ({ sessionId, roundId }) => {
        try {
          const data = await pokerSessionsWithdrawVote(sessionId, roundId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    updatePokerRoundLabel: builder.mutation<
      void,
      {
        sessionId: string
        roundId: string
        sessionKey: number
        request: UpdatePokerRoundLabelRequest
      }
    >({
      queryFn: async ({ sessionId, roundId, request }) => {
        try {
          const data = await pokerSessionsUpdateRoundLabel(
            sessionId,
            roundId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    updatePokerRoundDescription: builder.mutation<
      void,
      {
        sessionId: string
        roundId: string
        sessionKey: number
        request: UpdatePokerRoundDescriptionRequest
      }
    >({
      queryFn: async ({ sessionId, roundId, request }) => {
        try {
          const data = await pokerSessionsUpdateRoundDescription(
            sessionId,
            roundId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    focusPokerRound: builder.mutation<
      void,
      {
        sessionId: string
        sessionKey: number
        /** The round to show followers, or undefined for the round in play. */
        roundId: string | undefined
      }
    >({
      queryFn: async ({ sessionId, roundId }) => {
        try {
          const data = await pokerSessionsFocusRound(sessionId, {
            roundId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The facilitator's own screen moves at once. The refetch that follows
      // settles it against any older read of the session still in flight,
      // which would otherwise put the previous round back.
      onQueryStarted: async (
        { sessionKey, roundId },
        { dispatch, getState, queryFulfilled },
      ) => {
        const patches = setCachedFocusedRound(
          dispatch,
          getState(),
          sessionKey,
          roundId,
        )
        try {
          await queryFulfilled
        } catch {
          patches.forEach((patch) => patch.undo())
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSessionRound, id: arg.sessionKey },
      ],
    }),

    getPokerSessionInvitedTeamMembers: builder.query<
      EmployeeNavigationDto[],
      { sessionId: string; teamId: string }
    >({
      queryFn: async ({ sessionId, teamId }) => {
        try {
          const data = await pokerSessionsGetInvitedTeamMembers(
            sessionId,
            teamId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The session mutations invalidate this tag, so a changed invite refetches.
      providesTags: () => [{ type: QueryTags.PokerSession, id: 'LIST' }],
    }),

    setPokerSessionVisibility: builder.mutation<
      void,
      { id: string; key: number; visibility: Visibility }
    >({
      queryFn: async ({ id, visibility }) => {
        try {
          const data = await pokerSessionsSetVisibility(id, {
            visibility,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: 'LIST' },
        { type: QueryTags.PokerSession, id: arg.key },
      ],
    }),

    setPokerSessionFacilitators: builder.mutation<
      void,
      { id: string; key: number; employeeIds: string[] }
    >({
      queryFn: async ({ id, employeeIds }) => {
        try {
          const data = await pokerSessionsSetFacilitators(id, {
            employeeIds,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: 'LIST' },
        { type: QueryTags.PokerSession, id: arg.key },
      ],
    }),

    setPokerSessionInvitees: builder.mutation<
      void,
      { id: string; key: number; employeeIds: string[]; teamIds: string[] }
    >({
      queryFn: async ({ id, employeeIds, teamIds }) => {
        try {
          const data = await pokerSessionsSetInvitees(id, {
            employeeIds,
            teamIds,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: 'LIST' },
        { type: QueryTags.PokerSession, id: arg.key },
      ],
    }),

    joinPokerSession: builder.mutation<
      PokerSessionMemberDto,
      { id: string; key: number; isVoting?: boolean }
    >({
      queryFn: async ({ id, isVoting = true }) => {
        try {
          const data = await pokerSessionsJoin(id, { isVoting })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: 'LIST' },
        { type: QueryTags.PokerSession, id: arg.key },
      ],
    }),

    setPokerSessionVoting: builder.mutation<
      PokerSessionMemberDto,
      { id: string; key: number; isVoting: boolean }
    >({
      queryFn: async ({ id, isVoting }) => {
        try {
          const data = await pokerSessionsSetVoting(id, {
            isVoting,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PokerSession, id: 'LIST' },
        { type: QueryTags.PokerSession, id: arg.key },
      ],
    }),
  }),
})

type PokerSessionsApiState = Parameters<
  typeof pokerSessionsApi.util.selectInvalidatedBy
>[0]

/**
 * Writes the focused round into every cached copy of a session, so a screen
 * following the facilitator moves without waiting for a refetch.
 *
 * @returns The patches made, each of which can be undone.
 */
export const setCachedFocusedRound = (
  dispatch: ThunkDispatch<PokerSessionsApiState, unknown, UnknownAction>,
  state: PokerSessionsApiState,
  sessionKey: number,
  roundId: string | undefined,
): { undo: () => void }[] =>
  pokerSessionsApi.util
    .selectInvalidatedBy(state, [
      { type: QueryTags.PokerSessionRound, id: sessionKey },
    ])
    .filter((entry) => entry.endpointName === 'getPokerSession')
    .map((entry) =>
      dispatch(
        pokerSessionsApi.util.updateQueryData(
          'getPokerSession',
          entry.originalArgs,
          (draft) => {
            draft.focusedRoundId = roundId
          },
        ),
      ),
    )

export const {
  useGetPokerSessionsQuery,
  useGetPokerSessionQuery,
  useGetHostedPokerSessionsQuery,
  usePublishPokerRoundsMutation,
  useCreatePokerSessionMutation,
  useUpdatePokerSessionMutation,
  useDeletePokerSessionMutation,
  useCompletePokerSessionMutation,
  useAddPokerRoundMutation,
  useRemovePokerRoundMutation,
  useRevealPokerRoundMutation,
  useResetPokerRoundMutation,
  useSetPokerRoundConsensusMutation,
  useSubmitPokerVoteMutation,
  useWithdrawPokerVoteMutation,
  useUpdatePokerRoundLabelMutation,
  useUpdatePokerRoundDescriptionMutation,
  useFocusPokerRoundMutation,
  useGetPokerSessionInvitedTeamMembersQuery,
  useSetPokerSessionVisibilityMutation,
  useSetPokerSessionFacilitatorsMutation,
  useSetPokerSessionInviteesMutation,
  useJoinPokerSessionMutation,
  useSetPokerSessionVotingMutation,
} = pokerSessionsApi
