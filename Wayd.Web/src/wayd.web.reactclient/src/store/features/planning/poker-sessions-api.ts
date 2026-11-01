import { getPokerSessionsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  AddPokerRoundRequest,
  CreatePokerSessionRequest,
  ObjectIdAndKey,
  PokerRoundDto,
  PokerSessionDetailsDto,
  PokerSessionListDto,
  PokerSessionStatus,
  SetConsensusRequest,
  SubmitVoteRequest,
  UpdatePokerRoundDescriptionRequest,
  UpdatePokerRoundLabelRequest,
} from '@/src/services/wayd-api'

export interface UpdatePokerSessionRequest {
  name: string
  estimationScaleId: number
}

export const pokerSessionsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPokerSessions: builder.query<
      PokerSessionListDto[],
      PokerSessionStatus | undefined
    >({
      queryFn: async (status) => {
        try {
          const data = await getPokerSessionsClient().getList(status)
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
          const data = await getPokerSessionsClient().getSession(idOrKey)
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
          const data = await getPokerSessionsClient().create(request)
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
          const data = await getPokerSessionsClient().update(id, request)
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
          const data = await getPokerSessionsClient().delete(id)
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
          const data = await getPokerSessionsClient().complete(id)
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
          const data = await getPokerSessionsClient().addRound(
            sessionId,
            request,
          )
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
          const data = await getPokerSessionsClient().removeRound(
            sessionId,
            roundId,
          )
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
          const data = await getPokerSessionsClient().revealRound(
            sessionId,
            roundId,
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

    resetPokerRound: builder.mutation<
      void,
      { sessionId: string; roundId: string; sessionKey: number }
    >({
      queryFn: async ({ sessionId, roundId }) => {
        try {
          const data = await getPokerSessionsClient().resetRound(
            sessionId,
            roundId,
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
          const data = await getPokerSessionsClient().setConsensus(
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
          const data = await getPokerSessionsClient().submitVote(
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
          const data = await getPokerSessionsClient().withdrawVote(
            sessionId,
            roundId,
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
          const data = await getPokerSessionsClient().updateRoundLabel(
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
          const data = await getPokerSessionsClient().updateRoundDescription(
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
  }),
})

export const {
  useGetPokerSessionsQuery,
  useGetPokerSessionQuery,
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
} = pokerSessionsApi
