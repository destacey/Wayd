import {
  incidentSeveritySetsCreate,
  incidentSeveritySetsDelete,
  incidentSeveritySetsGetSets,
  incidentSeveritySetsSelectSet,
  incidentSeveritySetsSetActiveStatus,
  incidentSeveritySetsUpdate,
} from '@/src/services/api/incident-severity-sets'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateIncidentSeveritySetRequest,
  IncidentSeveritySetDto,
  SelectIncidentSeveritySetRequest,
  SetIncidentSeveritySetActiveStatusRequest,
  UpdateIncidentSeveritySetRequest,
} from '@/src/services/api/wayd.schemas'

export const incidentSeveritySetsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getIncidentSeveritySets: builder.query<
      IncidentSeveritySetDto[],
      boolean | void
    >({
      queryFn: async (includeInactive) => {
        try {
          const data = await incidentSeveritySetsGetSets({
            includeInactive: includeInactive || undefined,
          })
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
          const data = await incidentSeveritySetsCreate(request)
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
          const data = await incidentSeveritySetsUpdate(request.id, request)
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

    setIncidentSeveritySetActiveStatus: builder.mutation<
      void,
      SetIncidentSeveritySetActiveStatusRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await incidentSeveritySetsSetActiveStatus(
            request.id,
            request,
          )
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

    selectIncidentSeveritySet: builder.mutation<
      void,
      SelectIncidentSeveritySetRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await incidentSeveritySetsSelectSet(request)
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

    deleteIncidentSeveritySet: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await incidentSeveritySetsDelete(id)
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
