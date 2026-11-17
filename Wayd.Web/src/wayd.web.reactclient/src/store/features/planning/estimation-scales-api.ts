import {
  estimationScalesCreate,
  estimationScalesDelete,
  estimationScalesGetScale,
  estimationScalesGetScales,
  estimationScalesSetActiveStatus,
  estimationScalesUpdate,
} from '@/src/services/api/estimation-scales'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateEstimationScaleRequest,
  EstimationScaleDetailsDto,
  EstimationScaleDto,
  ObjectIdAndKey,
  UpdateEstimationScaleRequest,
} from '@/src/services/api/wayd.schemas'

export const estimationScalesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getEstimationScales: builder.query<
      EstimationScaleDto[],
      boolean | void
    >({
      queryFn: async (includeInactive) => {
        try {
          const data = await estimationScalesGetScales({
            includeInactive: includeInactive || undefined,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.EstimationScale, id: 'LIST' }],
    }),

    getEstimationScale: builder.query<EstimationScaleDetailsDto, string>({
      queryFn: async (idOrKey: string) => {
        try {
          const data = await estimationScalesGetScale(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        { type: QueryTags.EstimationScale, id: result?.id },
      ],
    }),

    createEstimationScale: builder.mutation<
      ObjectIdAndKey,
      CreateEstimationScaleRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await estimationScalesCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.EstimationScale, id: 'LIST' }],
    }),

    updateEstimationScale: builder.mutation<void, UpdateEstimationScaleRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await estimationScalesUpdate(request.id, request)
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: (result, error, arg) => [
          { type: QueryTags.EstimationScale, id: 'LIST' },
          { type: QueryTags.EstimationScale, id: arg.id },
        ],
      },
    ),

    setEstimationScaleActiveStatus: builder.mutation<
      void,
      { id: string; isActive: boolean }
    >({
      queryFn: async (request) => {
        try {
          const data = await estimationScalesSetActiveStatus(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.EstimationScale, id: 'LIST' },
        { type: QueryTags.EstimationScale, id: arg.id },
      ],
    }),

    deleteEstimationScale: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await estimationScalesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.EstimationScale, id: 'LIST' }],
    }),
  }),
})

export const {
  useGetEstimationScalesQuery,
  useGetEstimationScaleQuery,
  useCreateEstimationScaleMutation,
  useUpdateEstimationScaleMutation,
  useSetEstimationScaleActiveStatusMutation,
  useDeleteEstimationScaleMutation,
} = estimationScalesApi
