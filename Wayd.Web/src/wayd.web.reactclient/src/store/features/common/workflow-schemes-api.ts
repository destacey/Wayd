import {
  ChangeWorkflowSchemeDefaultRequest,
  CloneWorkflowSchemeRequest,
  CreateRecordTypeRequest,
  CreateWorkflowSchemeRequest,
  RecordTypeDto,
  RenameRecordTypeRequest,
  SetWorkflowSchemeEntryRequest,
  UpdateWorkflowSchemeRequest,
  WorkflowSchemeChangePreviewDto,
  WorkflowSchemeDetailsDto,
  WorkflowSchemeListDto,
} from '@/src/services/api/wayd.schemas'
import {
  recordTypesActivate,
  recordTypesCreate,
  recordTypesDeactivate,
  recordTypesGetRecordTypes,
  recordTypesRename,
} from '@/src/services/api/record-types'
import {
  workflowSchemesChangeDefault,
  workflowSchemesClone,
  workflowSchemesCreate,
  workflowSchemesDelete,
  workflowSchemesGetWorkflowScheme,
  workflowSchemesGetWorkflowSchemes,
  workflowSchemesPreviewDefault,
  workflowSchemesPreviewEntry,
  workflowSchemesSetEntry,
  workflowSchemesUpdate,
} from '@/src/services/api/workflow-schemes'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

const schemeTags = (id: string) => [
  { type: QueryTags.WorkflowScheme, id: 'LIST' },
  { type: QueryTags.WorkflowScheme, id },
]

/**
 * A change that moves records rewrites their statuses and histories, and
 * changes which workflows are in use, which every workflow's details report.
 */
const recordMoveTags = (id: string) => [
  ...schemeTags(id),
  { type: QueryTags.WorkflowAssignment, id: 'LIST' },
  { type: QueryTags.StatusWorkflow },
  { type: QueryTags.StatusHistory },
]

export const workflowSchemesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getRecordTypes: builder.query<RecordTypeDto[], string | undefined>({
      queryFn: async (ownerType) => {
        try {
          const data = await recordTypesGetRecordTypes({ ownerType: ownerType })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.RecordType, id: 'LIST' }],
    }),

    createRecordType: builder.mutation<string, CreateRecordTypeRequest>({
      queryFn: async (request) => {
        try {
          const data = await recordTypesCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.RecordType, id: 'LIST' }],
    }),

    renameRecordType: builder.mutation<
      void,
      { id: string; request: RenameRecordTypeRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await recordTypesRename(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A scheme's entries are listed by record type name.
      invalidatesTags: () => [
        { type: QueryTags.RecordType, id: 'LIST' },
        { type: QueryTags.WorkflowScheme },
      ],
    }),

    setRecordTypeActive: builder.mutation<
      void,
      { id: string; isActive: boolean }
    >({
      queryFn: async ({ id, isActive }) => {
        try {
          const data = isActive
            ? await recordTypesActivate(id)
            : await recordTypesDeactivate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.RecordType, id: 'LIST' }],
    }),

    getWorkflowSchemes: builder.query<
      WorkflowSchemeListDto[],
      string | undefined
    >({
      queryFn: async (ownerType) => {
        try {
          const data = await workflowSchemesGetWorkflowSchemes({
            ownerType: ownerType,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.WorkflowScheme, id: 'LIST' }],
    }),

    getWorkflowScheme: builder.query<WorkflowSchemeDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await workflowSchemesGetWorkflowScheme(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Fetched by key, invalidated by id, so tagged with both.
      providesTags: (result, error, arg) => [
        ...(result ? [{ type: QueryTags.WorkflowScheme, id: result.id }] : []),
        { type: QueryTags.WorkflowScheme, id: arg },
      ],
    }),

    createWorkflowScheme: builder.mutation<string, CreateWorkflowSchemeRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await workflowSchemesCreate(request)
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: () => [{ type: QueryTags.WorkflowScheme, id: 'LIST' }],
      },
    ),

    updateWorkflowScheme: builder.mutation<
      void,
      { id: string; request: UpdateWorkflowSchemeRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await workflowSchemesUpdate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => schemeTags(arg.id),
    }),

    cloneWorkflowScheme: builder.mutation<
      string,
      { id: string; request: CloneWorkflowSchemeRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await workflowSchemesClone(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.WorkflowScheme, id: 'LIST' }],
    }),

    deleteWorkflowScheme: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await workflowSchemesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The list only: invalidating the deleted scheme's own tag would refetch
      // the details page still showing it, which now 404s.
      invalidatesTags: () => [{ type: QueryTags.WorkflowScheme, id: 'LIST' }],
    }),

    previewWorkflowSchemeEntry: builder.query<
      WorkflowSchemeChangePreviewDto,
      { schemeId: string; recordTypeId: string; workflowId?: string }
    >({
      queryFn: async ({ schemeId, recordTypeId, workflowId }) => {
        try {
          const data = await workflowSchemesPreviewEntry(
            schemeId,
            recordTypeId,
            { workflowId: workflowId },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Counts go stale as records move, so never served from cache.
      keepUnusedDataFor: 0,
    }),

    previewWorkflowSchemeDefault: builder.query<
      WorkflowSchemeChangePreviewDto,
      { schemeId: string; workflowId: string }
    >({
      queryFn: async ({ schemeId, workflowId }) => {
        try {
          const data = await workflowSchemesPreviewDefault(schemeId, {
            workflowId: workflowId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      keepUnusedDataFor: 0,
    }),

    setWorkflowSchemeEntry: builder.mutation<
      number,
      {
        schemeId: string
        recordTypeId: string
        request: SetWorkflowSchemeEntryRequest
      }
    >({
      queryFn: async ({ schemeId, recordTypeId, request }) => {
        try {
          const data = await workflowSchemesSetEntry(
            schemeId,
            recordTypeId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => recordMoveTags(arg.schemeId),
    }),

    changeWorkflowSchemeDefault: builder.mutation<
      number,
      { schemeId: string; request: ChangeWorkflowSchemeDefaultRequest }
    >({
      queryFn: async ({ schemeId, request }) => {
        try {
          const data = await workflowSchemesChangeDefault(schemeId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => recordMoveTags(arg.schemeId),
    }),
  }),
})

export const {
  useGetRecordTypesQuery,
  useCreateRecordTypeMutation,
  useRenameRecordTypeMutation,
  useSetRecordTypeActiveMutation,
  useGetWorkflowSchemesQuery,
  useGetWorkflowSchemeQuery,
  useCreateWorkflowSchemeMutation,
  useUpdateWorkflowSchemeMutation,
  useCloneWorkflowSchemeMutation,
  useDeleteWorkflowSchemeMutation,
  usePreviewWorkflowSchemeEntryQuery,
  usePreviewWorkflowSchemeDefaultQuery,
  useSetWorkflowSchemeEntryMutation,
  useChangeWorkflowSchemeDefaultMutation,
} = workflowSchemesApi
