import { getIncidentsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import type {
  CreateIncidentRequest,
  IncidentDto,
  ManageIncidentWorkItemsRequest,
  IncidentListDto,
  ObjectIdAndKey,
  CreateIncidentTeamsChatRequest,
  LinkIncidentTeamsChatRequest,
  UpdateIncidentDetailsRequest,
  WorkItemsSummaryDto,
  AddIncidentTimelineNoteRequest,
  IncidentTimelineNoteDto,
  IncidentTimelineLinkEntryDto,
  IncidentWorkflowActionDto,
  ApplyIncidentWorkflowActionRequest,
  ProductStatusAlias,
  StatusWorkflowDetailsDto,
} from '@/src/services/wayd-api'

export interface GetIncidentsRequest {
  page?: number
  pageSize?: number
  statuses?: ProductStatusAlias[]
  severityLevelIds?: string[]
}

export const incidentsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getIncidents: builder.query<
      IncidentListDto,
      GetIncidentsRequest | undefined
    >({
      queryFn: async (args) => {
        try {
          const data = await getIncidentsClient().getIncidents(
            args?.page,
            args?.pageSize,
            args?.statuses,
            args?.severityLevelIds,
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
      // Tag by both the resolved id and the route key. Mutations invalidate the
      // id, and a key-only tag never refetches the page that loaded by key.
      providesTags: (result, _error, idOrKey) => [
        ...(result ? [{ type: QueryTags.Incident, id: result.id }] : []),
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
    createIncidentTeamsChat: builder.mutation<
      void,
      CreateIncidentTeamsChatRequest
    >({
      queryFn: async (request) => {
        try {
          await getIncidentsClient().createTeamsChat(request.id, request)
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, request) => [
        { type: QueryTags.Incident, id: request.id },
      ],
    }),
    linkIncidentTeamsChat: builder.mutation<void, LinkIncidentTeamsChatRequest>(
      {
        queryFn: async (request) => {
          try {
            await getIncidentsClient().linkTeamsChat(request.id, request)
            return { data: undefined }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: (_result, _error, request) => [
          { type: QueryTags.Incident, id: request.id },
        ],
      },
    ),
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
          const data = await getIncidentsClient().getIncidentWorkItems(idOrKey)
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
        {
          type: QueryTags.IncidentTimelineLinkEntries,
          id: request.incidentId,
        },
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
    getIncidentTimelineLinkEntries: builder.query<
      IncidentTimelineLinkEntryDto[],
      { idOrKey: string; incidentId: string }
    >({
      queryFn: async ({ idOrKey }) => {
        try {
          const data =
            await getIncidentsClient().getIncidentTimelineLinkEntries(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { incidentId }) => [
        { type: QueryTags.IncidentTimelineLinkEntries, id: incidentId },
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
        { type: QueryTags.Incident, id: request.incidentId },
      ],
    }),
    getIncidentStatusWorkflow: builder.query<StatusWorkflowDetailsDto, string>({
      queryFn: async (incidentId) => {
        try {
          const data = await getIncidentsClient().getStatusWorkflow(incidentId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, _error, incidentId) => [
        ...(result
          ? [{ type: QueryTags.StatusWorkflow as const, id: result.id }]
          : []),
        { type: QueryTags.Incident, id: incidentId },
      ],
    }),
    getIncidentWorkflowActions: builder.query<
      IncidentWorkflowActionDto[],
      string
    >({
      queryFn: async (idOrKey) => {
        try {
          const data =
            await getIncidentsClient().getIncidentWorkflowActions(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, idOrKey) => [
        { type: QueryTags.WorkflowActions, id: idOrKey },
      ],
    }),
    applyIncidentWorkflowAction: builder.mutation<
      void,
      {
        id: string
        idOrKey: string
        request: ApplyIncidentWorkflowActionRequest
      }
    >({
      queryFn: async ({ id, request }) => {
        try {
          await getIncidentsClient().applyIncidentWorkflowAction(id, request)
          return { data: undefined }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { id, idOrKey }) => [
        { type: QueryTags.Incident, id },
        { type: QueryTags.Incident, id: idOrKey },
        { type: QueryTags.Incident, id: 'LIST' },
        { type: QueryTags.WorkflowActions, id: idOrKey },
      ],
    }),
  }),
})

export const {
  useGetIncidentsQuery,
  useGetIncidentQuery,
  useCreateIncidentMutation,
  useCreateIncidentTeamsChatMutation,
  useLinkIncidentTeamsChatMutation,
  useUpdateIncidentDetailsMutation,
  useGetIncidentWorkItemsQuery,
  useManageIncidentWorkItemsMutation,
  useGetIncidentTimelineNotesQuery,
  useGetIncidentTimelineLinkEntriesQuery,
  useAddIncidentTimelineNoteMutation,
  useGetIncidentStatusWorkflowQuery,
  useGetIncidentWorkflowActionsQuery,
  useApplyIncidentWorkflowActionMutation,
} = incidentsApi
