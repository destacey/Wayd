import { toInstant } from '@/src/utils/instant'
import {
  DeadLetterMessageDetailsResponse,
  MessagingCountsResponse,
  PagedResponseOfDeadLetterMessageResponse,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  messagingDiscardDeadLetters,
  messagingGetCounts,
  messagingGetDeadLetterById,
  messagingGetDeadLetters,
  messagingReplayDeadLetters,
} from '@/src/services/api/messaging'

export interface GetDeadLettersRequest {
  messageType?: string
  exceptionType?: string
  from?: Date
  to?: Date
  /** 1-based. */
  page?: number
  pageSize?: number
}

export const messagingApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getMessagingCounts: builder.query<MessagingCountsResponse, void>({
      queryFn: async () => {
        try {
          const data = await messagingGetCounts()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MessagingCounts],
    }),
    getDeadLetters: builder.query<
      PagedResponseOfDeadLetterMessageResponse,
      GetDeadLettersRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await messagingGetDeadLetters({
            messageType: request.messageType,
            exceptionType: request.exceptionType,
            from: request.from ? toInstant(request.from) : undefined,
            to: request.to ? toInstant(request.to) : undefined,
            page: request.page,
            pageSize: request.pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.DeadLetterMessage,
        ...(result?.items?.map(({ id }) => ({
          type: QueryTags.DeadLetterMessage,
          id,
        })) ?? []),
      ],
    }),
    getDeadLetterById: builder.query<DeadLetterMessageDetailsResponse, string>({
      queryFn: async (id) => {
        try {
          const data = await messagingGetDeadLetterById(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, id) => [
        { type: QueryTags.DeadLetterMessage, id },
      ],
    }),
    replayDeadLetters: builder.mutation<void, string[]>({
      queryFn: async (ids) => {
        try {
          const data = await messagingReplayDeadLetters({ ids })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [QueryTags.DeadLetterMessage, QueryTags.MessagingCounts],
    }),
    discardDeadLetters: builder.mutation<void, string[]>({
      queryFn: async (ids) => {
        try {
          const data = await messagingDiscardDeadLetters({ ids })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [QueryTags.DeadLetterMessage, QueryTags.MessagingCounts],
    }),
  }),
})

export const {
  useGetMessagingCountsQuery,
  useGetDeadLettersQuery,
  useGetDeadLetterByIdQuery,
  useReplayDeadLettersMutation,
  useDiscardDeadLettersMutation,
} = messagingApi
