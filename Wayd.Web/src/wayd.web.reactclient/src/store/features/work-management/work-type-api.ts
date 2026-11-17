import {
  workTypesGetById,
  workTypesGetList,
  workTypesUpdate,
} from '@/src/services/api/work-types'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  UpdateWorkTypeRequest,
  WorkTypeDto,
} from '@/src/services/api/wayd.schemas'

export const workTypeApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getWorkTypes: builder.query<WorkTypeDto[], boolean>({
      queryFn: async (includeInactive) => {
        try {
          const data = await workTypesGetList({
            includeInactive: includeInactive,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkType,
        ...(result?.map(({ id }) => ({ type: QueryTags.WorkType, id })) ?? []),
      ],
    }),
    getWorkType: builder.query<WorkTypeDto, number>({
      queryFn: async (id: number) => {
        try {
          const data = await workTypesGetById(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [{ type: QueryTags.WorkType, id: result?.id }],
    }),
    updateWorkType: builder.mutation<void, UpdateWorkTypeRequest>({
      queryFn: async (request) => {
        try {
          const data = await workTypesUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.WorkType, id: arg.id },
      ],
    }),
  }),
  overrideExisting: false,
})

export const {
  useGetWorkTypesQuery,
  useGetWorkTypeQuery,
  useUpdateWorkTypeMutation,
} = workTypeApi
