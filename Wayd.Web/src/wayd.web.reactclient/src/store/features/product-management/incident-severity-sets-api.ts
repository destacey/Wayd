import { getIncidentSeveritySetsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateIncidentSeveritySetRequest,
  IncidentSeveritySetDto,
  SelectIncidentSeveritySetRequest,
  SetIncidentSeveritySetActiveStatusRequest,
  UpdateIncidentSeveritySetRequest,
} from '@/src/services/wayd-api'

export const incidentSeveritySetsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getIncidentSeveritySets: builder.query<
      IncidentSeveritySetDto[],
      boolean | void
    >({
      queryFn: async (includeInactive) => {
        try {
          const data = await getIncidentSeveritySetsClient().getSets(
            includeInactive || undefined,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [
        { type: QueryTags.IncidentSeveritySet, id: 'LIST' },
      ],
    }),

    createIncidentSeveritySet: builder.mutation<
      string,
      CreateIncidentSeveritySetRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getIncidentSeveritySetsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.IncidentSeveritySet, id: 'LIST' },
      ],
    }),

    updateIncidentSeveritySet: builder.mutation<
      void,
      UpdateIncidentSeveritySetRequest
    >({
      queryFn: async (request) => {
        try {
          await getIncidentSeveritySetsClient().update(request.id, request)
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.IncidentSeveritySet, id: 'LIST' },
      ],
    }),

    setIncidentSeveritySetActiveStatus: builder.mutation<
      void,
      SetIncidentSeveritySetActiveStatusRequest
    >({
      queryFn: async (request) => {
        try {
          await getIncidentSeveritySetsClient().setActiveStatus(
            request.id,
            request,
          )
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.IncidentSeveritySet, id: 'LIST' },
      ],
    }),

    selectIncidentSeveritySet: builder.mutation<
      void,
      SelectIncidentSeveritySetRequest
    >({
      queryFn: async (request) => {
        try {
          await getIncidentSeveritySetsClient().selectSet(request)
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.IncidentSeveritySet, id: 'LIST' },
      ],
    }),

    deleteIncidentSeveritySet: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          await getIncidentSeveritySetsClient().delete(id)
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.IncidentSeveritySet, id: 'LIST' },
      ],
    }),
  }),
})

export const {
  useGetIncidentSeveritySetsQuery,
  useCreateIncidentSeveritySetMutation,
  useUpdateIncidentSeveritySetMutation,
  useSetIncidentSeveritySetActiveStatusMutation,
  useSelectIncidentSeveritySetMutation,
  useDeleteIncidentSeveritySetMutation,
} = incidentSeveritySetsApi
