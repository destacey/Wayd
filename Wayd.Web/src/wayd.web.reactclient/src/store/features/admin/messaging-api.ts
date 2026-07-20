import {
  DeadLetterMessageDetailsResponse,
  DeadLetterMessagesResponse,
  MessagingCountsResponse,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { getMessagingClient } from '@/src/services/clients'

export interface GetDeadLettersRequest {
  messageType?: string
  exceptionType?: string
  from?: Date
  to?: Date
  /** 0-based, mirroring Wolverine's dead letter query paging. */
  pageNumber?: number
  pageSize?: number
}

export const messagingApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getMessagingCounts: builder.query<MessagingCountsResponse, void>({
      queryFn: async () => {
        try {
          const data = await getMessagingClient().getCounts()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MessagingCounts],
    }),
    getDeadLetters: builder.query<DeadLetterMessagesResponse, GetDeadLettersRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await getMessagingClient().getDeadLetters(
              request.messageType,
              request.exceptionType,
              request.from,
              request.to,
              request.pageNumber,
              request.pageSize,
            )
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
      },
    ),
    getDeadLetterById: builder.query<DeadLetterMessageDetailsResponse, string>({
      queryFn: async (id) => {
        try {
          const data = await getMessagingClient().getDeadLetterById(id)
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
          const data = await getMessagingClient().replayDeadLetters({ ids })
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
          const data = await getMessagingClient().discardDeadLetters({ ids })
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
