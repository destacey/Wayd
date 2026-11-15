import {
  incidentsAddIncidentTimelineNote,
  incidentsApplyIncidentWorkflowAction,
  incidentsCreate,
  incidentsCreateTeamsChat,
  incidentsGetIncident,
  incidentsGetIncidentFigures,
  incidentsGetIncidentTimelineLinkEntries,
  incidentsGetIncidentTimelineNotes,
  incidentsGetIncidentWorkItems,
  incidentsGetIncidentWorkflowActions,
  incidentsGetIncidents,
  incidentsGetStatusWorkflow,
  incidentsLinkTeamsChat,
  incidentsManageIncidentWorkItems,
  incidentsUpdateDetails,
} from '@/src/services/api/incidents'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import type {
  CreateIncidentRequest,
  IncidentDto,
  ManageIncidentWorkItemsRequest,
  IncidentFiguresDto,
  PagedResponseOfIncidentSummaryDto,
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
} from '@/src/services/api/wayd.schemas'

export interface GetIncidentsRequest {
  page?: number
  pageSize?: number
  statuses?: ProductStatusAlias[]
  severityLevelIds?: string[]
}

export const incidentsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getIncidents: builder.query<
      PagedResponseOfIncidentSummaryDto,
      GetIncidentsRequest | undefined
    >({
      queryFn: async (args) => {
        try {
          const data = await incidentsGetIncidents({
            page: args?.page,
            pageSize: args?.pageSize,
            status: args?.statuses,
            severityLevelId: args?.severityLevelIds,
          })
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
    getIncidentFigures: builder.query<
      IncidentFiguresDto,
      Pick<GetIncidentsRequest, 'statuses' | 'severityLevelIds'> | undefined
    >({
      queryFn: async (args) => {
        try {
          const data = await incidentsGetIncidentFigures({
            status: args?.statuses,
            severityLevelId: args?.severityLevelIds,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Same tag as the list, so creating or changing an incident refreshes the
      // figures. The args omit the page, so paging the grid does not.
      providesTags: [{ type: QueryTags.Incident, id: 'LIST' }],
    }),
    getIncident: builder.query<IncidentDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await incidentsGetIncident(idOrKey)
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
          const data = await incidentsCreate(request)
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
          await incidentsCreateTeamsChat(request.id, request)
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
            await incidentsLinkTeamsChat(request.id, request)
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
            await incidentsUpdateDetails(request.id, request)
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
          const data = await incidentsGetIncidentWorkItems(idOrKey)
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
          await incidentsManageIncidentWorkItems(request.incidentId, request)
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
          const data = await incidentsGetIncidentTimelineNotes(idOrKey)
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
          const data = await incidentsGetIncidentTimelineLinkEntries(idOrKey)
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
          const data = await incidentsAddIncidentTimelineNote(
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
          const data = await incidentsGetStatusWorkflow(incidentId)
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
          const data = await incidentsGetIncidentWorkflowActions(idOrKey)
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
          await incidentsApplyIncidentWorkflowAction(id, request)
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
  useGetIncidentFiguresQuery,
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
