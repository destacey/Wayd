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
  pageNumber?: number
  pageSize?: number
}

export interface GetImportProcessRowsRequest {
  importProcessId: string
  status?: ImportRowStatus
  pageNumber?: number
  pageSize?: number
}

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
