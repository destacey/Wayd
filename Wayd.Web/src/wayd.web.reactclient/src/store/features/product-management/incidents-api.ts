import { getIncidentsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import type {
  CreateIncidentRequest,
  IncidentDto,
  ManageIncidentWorkItemsRequest,
  ObjectIdAndKey,
  PagedResponseOfIncidentSummaryDto,
  UpdateIncidentDetailsRequest,
  WorkItemsSummaryDto,
  AddIncidentTimelineNoteRequest,
  IncidentTimelineNoteDto,
} from '@/src/services/wayd-api'

export interface GetIncidentsRequest {
  page?: number
  pageSize?: number
}

export const incidentsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getIncidents: builder.query<
      PagedResponseOfIncidentSummaryDto,
      GetIncidentsRequest | undefined
    >({
      queryFn: async (args) => {
        try {
          const data = await getIncidentsClient().getIncidents(
            args?.page,
            args?.pageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) =>
        result?.items
          ? [
              ...result.items.map(({ id }) => ({
                type: QueryTags.Incident as const,
                id,
              })),
              { type: QueryTags.Incident, id: 'LIST' },
            ]
          : [{ type: QueryTags.Incident, id: 'LIST' }],
    }),
    getIncident: builder.query<IncidentDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getIncidentsClient().getIncident(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, idOrKey) => [
        { type: QueryTags.Incident, id: idOrKey },
      ],
    }),
    createIncident: builder.mutation<ObjectIdAndKey, CreateIncidentRequest>({
      queryFn: async (request) => {
        try {
          const data = await getIncidentsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [{ type: QueryTags.Incident, id: 'LIST' }],
    }),
    updateIncidentDetails: builder.mutation<void, UpdateIncidentDetailsRequest>(
      {
        queryFn: async (request) => {
          try {
            await getIncidentsClient().updateDetails(request.id, request)
            return { data: undefined }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: (_result, _error, request) => [
          { type: QueryTags.Incident, id: request.id },
          { type: QueryTags.Incident, id: 'LIST' },
        ],
      },
    ),
    getIncidentWorkItems: builder.query<
      WorkItemsSummaryDto,
      { idOrKey: string; incidentId: string }
    >({
      queryFn: async ({ idOrKey }) => {
        try {
          const data =
            await getIncidentsClient().getIncidentWorkItems(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { incidentId }) => [
        { type: QueryTags.IncidentWorkItemsSummary, id: incidentId },
      ],
    }),
    manageIncidentWorkItems: builder.mutation<
      void,
      ManageIncidentWorkItemsRequest
    >({
      queryFn: async (request) => {
        try {
          await getIncidentsClient().manageIncidentWorkItems(
            request.incidentId,
            request,
          )
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, request) => [
        { type: QueryTags.IncidentWorkItemsSummary, id: request.incidentId },
      ],
    }),
    getIncidentTimelineNotes: builder.query<
      IncidentTimelineNoteDto[],
      { idOrKey: string; incidentId: string }
    >({
      queryFn: async ({ idOrKey }) => {
        try {
          const data =
            await getIncidentsClient().getIncidentTimelineNotes(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { incidentId }) => [
        { type: QueryTags.IncidentTimelineNotes, id: incidentId },
      ],
    }),
    addIncidentTimelineNote: builder.mutation<
      string,
      AddIncidentTimelineNoteRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getIncidentsClient().addIncidentTimelineNote(
            request.incidentId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, request) => [
        { type: QueryTags.IncidentTimelineNotes, id: request.incidentId },
      ],
    }),
  }),
})

export const {
  useGetIncidentsQuery,
  useGetIncidentQuery,
  useCreateIncidentMutation,
  useUpdateIncidentDetailsMutation,
  useGetIncidentWorkItemsQuery,
  useManageIncidentWorkItemsMutation,
  useGetIncidentTimelineNotesQuery,
  useAddIncidentTimelineNoteMutation,
} = incidentsApi
