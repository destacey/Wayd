import {
  ImportDefinitionDto,
  ImportProcessDto,
  ImportProcessPageDto,
  ImportProcessRowPageDto,
  ImportProcessStatus,
  ImportRowStatus,
  ResumedImport,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { getImportsClient } from '@/src/services/clients'

export interface GetImportProcessesRequest {
  status?: ImportProcessStatus
  importType?: string
  submittedByUserId?: string
  /** Only the runs of one batch — the files a caller posted together. */
  submissionGroupId?: string
  pageNumber?: number
  pageSize?: number
}

export interface GetImportProcessRowsRequest {
  importProcessId: string
  status?: ImportRowStatus
  pageNumber?: number
  pageSize?: number
  /**
   * Never sent — part of the cache key only, so the rows are read again each time the run moves on. The
   * last poll while a run is going can land just before it finishes, and nothing else would refetch them.
   */
  runStatus?: ImportProcessStatus
}

/**
 * The cached lists each import type writes to. An import's own mutation refreshes its list when the
 * submission answers, which is enough for a run that finished within the wait. One that answered while
 * still running lands later, so whatever watches it finish refreshes these instead.
 */
const IMPORTED_RECORD_TAGS: Record<string, { type: QueryTags; id: string }[]> =
  {
    'product-management.products': [{ type: QueryTags.Product, id: 'LIST' }],
    'product-management.versions': [{ type: QueryTags.Version, id: 'LIST' }],
    'product-management.releases': [{ type: QueryTags.Release, id: 'LIST' }],
    'product-management.release-packages': [
      { type: QueryTags.ReleasePackage, id: 'LIST' },
    ],
  }

export const importedRecordTags = (importType: string) =>
  IMPORTED_RECORD_TAGS[importType] ?? []

export const importsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getImportProcesses: builder.query<
      ImportProcessPageDto,
      GetImportProcessesRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getImportsClient().getList(
            request.status,
            request.importType,
            request.submittedByUserId,
            request.submissionGroupId,
            request.pageNumber,
            request.pageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.ImportProcess,
        ...(result?.processes?.map(({ id }) => ({
          type: QueryTags.ImportProcess,
          id,
        })) ?? []),
      ],
    }),
    getImportProcessById: builder.query<ImportProcessDto, string>({
      queryFn: async (id) => {
        try {
          const data = await getImportsClient().getById(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, id) => [
        { type: QueryTags.ImportProcess, id },
      ],
    }),
    getImportProcessRows: builder.query<
      ImportProcessRowPageDto,
      GetImportProcessRowsRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getImportsClient().getRows(
            request.importProcessId,
            request.status,
            request.pageNumber,
            request.pageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, request) => [
        { type: QueryTags.ImportProcessRow, id: request.importProcessId },
      ],
    }),
    getImportDefinitions: builder.query<ImportDefinitionDto[], void>({
      queryFn: async () => {
        try {
          const data = await getImportsClient().getDefinitions()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.ImportDefinition],
    }),
    cancelImportProcess: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getImportsClient().cancel(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The run's rows change with it: a cancel settles everything it never reached.
      invalidatesTags: (result, error, id) => [
        QueryTags.ImportProcess,
        { type: QueryTags.ImportProcess, id },
        { type: QueryTags.ImportProcessRow, id },
      ],
    }),
    resumeImportProcess: builder.mutation<ResumedImport, string>({
      queryFn: async (id) => {
        try {
          const data = await getImportsClient().resume(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        QueryTags.ImportProcess,
        { type: QueryTags.ImportProcess, id },
        { type: QueryTags.ImportProcessRow, id },
      ],
    }),
    retryFailedImportRows: builder.mutation<ResumedImport, string>({
      queryFn: async (id) => {
        try {
          const data = await getImportsClient().retryFailed(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        QueryTags.ImportProcess,
        { type: QueryTags.ImportProcess, id },
        { type: QueryTags.ImportProcessRow, id },
      ],
    }),
  }),
})

export const {
  useGetImportProcessesQuery,
  useGetImportProcessByIdQuery,
  useGetImportProcessRowsQuery,
  useGetImportDefinitionsQuery,
  useCancelImportProcessMutation,
  useResumeImportProcessMutation,
  useRetryFailedImportRowsMutation,
} = importsApi
