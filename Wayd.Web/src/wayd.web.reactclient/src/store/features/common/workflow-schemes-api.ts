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
} from '@/src/services/wayd-api'
import {
  getRecordTypesClient,
  getWorkflowSchemesClient,
} from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

const schemeTags = (id: string) => [
  { type: QueryTags.WorkflowScheme, id: 'LIST' },
  { type: QueryTags.WorkflowScheme, id },
]

/**
 * A change that moves records rewrites their statuses, so the records' own
 * caches go stale along with the scheme.
 */
const recordMoveTags = (id: string) => [
  ...schemeTags(id),
  { type: QueryTags.WorkflowAssignment, id: 'LIST' },
  { type: QueryTags.StatusWorkflow, id: 'LIST' },
  { type: QueryTags.StatusHistory },
]

export const workflowSchemesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getRecordTypes: builder.query<RecordTypeDto[], string | undefined>({
      queryFn: async (ownerType) => {
        try {
          const data = await getRecordTypesClient().getRecordTypes(ownerType)
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
          const data = await getRecordTypesClient().create(request)
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
          const data = await getRecordTypesClient().rename(id, request)
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
          const client = getRecordTypesClient()
          const data = isActive
            ? await client.activate(id)
            : await client.deactivate(id)
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
          const data =
            await getWorkflowSchemesClient().getWorkflowSchemes(ownerType)
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
          const data =
            await getWorkflowSchemesClient().getWorkflowScheme(idOrKey)
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
            const data = await getWorkflowSchemesClient().create(request)
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
          const data = await getWorkflowSchemesClient().update(id, request)
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
          const data = await getWorkflowSchemesClient().clone(id, request)
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
          const data = await getWorkflowSchemesClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => schemeTags(id),
    }),

    previewWorkflowSchemeEntry: builder.query<
      WorkflowSchemeChangePreviewDto,
      { schemeId: string; recordTypeId: string; workflowId?: string }
    >({
      queryFn: async ({ schemeId, recordTypeId, workflowId }) => {
        try {
          const data = await getWorkflowSchemesClient().previewEntry(
            schemeId,
            recordTypeId,
            workflowId,
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
          const data = await getWorkflowSchemesClient().previewDefault(
            schemeId,
            workflowId,
          )
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
          const data = await getWorkflowSchemesClient().setEntry(
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
          const data = await getWorkflowSchemesClient().changeDefault(
            schemeId,
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
