import { UpdateWorkTypeLevelsOrderRequest } from './../../../services/wayd-api'
import {
  getWorkTypeLevelsClient,
  getWorkTypeTiersClient,
} from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateWorkTypeLevelRequest,
  UpdateWorkTypeLevelRequest,
  WorkTypeLevelDto,
} from '@/src/services/wayd-api'
import type { BaseOptionType } from 'antd/es/select'

export const workTypeLevelApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getWorkTypeLevels: builder.query<WorkTypeLevelDto[], null>({
      queryFn: async () => {
        try {
          const data = await getWorkTypeLevelsClient().getList()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkTypeLevel,
        ...(result?.map(({ id }) => ({ type: QueryTags.WorkTypeLevel, id })) ?? []),
      ],
    }),
    getWorkTypeLevelOptions: builder.query<BaseOptionType[], null>({
      queryFn: async () => {
        try {
          const [tiers, levels] = await Promise.all([
            getWorkTypeTiersClient().getList(),
            getWorkTypeLevelsClient().getList(),
          ])

          const data: BaseOptionType[] = tiers
            .sort((a, b) => a.order - b.order)
            .map((tier) => ({
              label: tier.name,
              options: levels
                .filter((level) => level.tier.id === tier.id)
                .sort((a, b) => a.order - b.order)
                .map((level) => ({
                  label: level.name,
                  value: level.id,
                })),
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkTypeLevelOption,
        ...(result?.map(({ id }) => ({
          type: QueryTags.WorkTypeLevelOption,
          id,
        })) ?? []),
      ],
    }),
    getWorkTypeLevel: builder.query<WorkTypeLevelDto, number>({
      queryFn: async (id: number) => {
        try {
          const data = await getWorkTypeLevelsClient().getById(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        { type: QueryTags.WorkTypeLevel, id: result?.id },
      ],
    }),
    createWorkTypeLevel: builder.mutation<number, CreateWorkTypeLevelRequest>({
      queryFn: async (request) => {
        try {
          const data = await getWorkTypeLevelsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        QueryTags.WorkTypeLevel,
        QueryTags.WorkTypeLevelOption,
      ],
    }),
    updateWorkTypeLevel: builder.mutation<null, UpdateWorkTypeLevelRequest>({
      queryFn: async (request) => {
        try {
          await getWorkTypeLevelsClient().update(request.id, request)
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.WorkTypeLevel, id: arg.id },
        { type: QueryTags.WorkTypeLevelOption, id: arg.id },
      ],
    }),
    updateWorkTypeLevelsOrder: builder.mutation<
      void,
      UpdateWorkTypeLevelsOrderRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getWorkTypeLevelsClient().updateOrder(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [QueryTags.WorkTypeLevel, QueryTags.WorkTypeLevelOption]
      },
    }),
  }),
  overrideExisting: false,
})

export const {
  useGetWorkTypeLevelsQuery,
  useGetWorkTypeLevelOptionsQuery,
  useGetWorkTypeLevelQuery,
  useCreateWorkTypeLevelMutation,
  useUpdateWorkTypeLevelMutation,
  useUpdateWorkTypeLevelsOrderMutation,
} = workTypeLevelApi
