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
import {
  getDeploymentEnvironmentsClient,
  getDeploymentsClient,
  getEmployeesClient,
  getImportsClient,
  getPlanningIntervalsClient,
  getPortfoliosClient,
  getProductsClient,
  getProgramsClient,
  getProjectsClient,
  getReleasePackagesClient,
  getReleasesClient,
  getRisksClient,
  getStrategicInitiativesClient,
  getStrategicThemesClient,
  getTeamsClient,
  getVersionsClient,
} from '@/src/services/clients'
import type { ImportKey } from '@/src/components/common/import/import-templates.generated'

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

export interface SubmitImportRequest {
  importKey: ImportKey
  /** The chosen files, keyed by the multipart field each is posted as. */
  files: Partial<Record<string, File>>
}

type ImportedRecordTag = QueryTags | { type: QueryTags; id: string }

/**
 * The cached records each import type writes to. The submission refreshes them when it answers, which is
 * enough for a run that finished within the wait. One that answered while still running lands later, so
 * whatever watches it finish refreshes these instead.
 *
 * Keyed by every generated import, so a new one cannot be offered on the Imports page without saying
 * what it changes. A bare tag refreshes every cached entry of that type — the lists and any record
 * open elsewhere — which is what a bulk write calls for where there is no single list tag.
 */
const IMPORTED_RECORD_TAGS: Record<ImportKey, ImportedRecordTag[]> = {
  employees: [QueryTags.Employee, QueryTags.EmployeeOption],
  teams: [QueryTags.Team, QueryTags.TeamOptions],
  'team-members': [QueryTags.TeamMembership, QueryTags.Team],
  'team-memberships': [QueryTags.TeamMembership, QueryTags.Team],
  'planning.planning-intervals': [QueryTags.PlanningInterval],
  'planning.planning-interval-objectives': [
    QueryTags.PlanningIntervalObjective,
  ],
  'planning.risks': [QueryTags.Risk, QueryTags.TeamRisk],
  'ppm.portfolios': [QueryTags.Portfolio],
  'ppm.programs': [QueryTags.Program, QueryTags.PortfolioPrograms],
  'ppm.projects': [
    QueryTags.Project,
    QueryTags.PortfolioProjects,
    QueryTags.ProgramProjects,
  ],
  'ppm.project-tasks': [
    QueryTags.ProjectTask,
    QueryTags.ProjectTaskTree,
    QueryTags.ProjectPlanTree,
  ],
  'ppm.project-stages': [QueryTags.ProjectPlanTree, QueryTags.ProjectTaskTree],
  'ppm.finalizations': [QueryTags.Portfolio, QueryTags.Program],
  'ppm.strategic-initiatives': [
    QueryTags.StrategicInitiative,
    QueryTags.StrategicInitiativeKpi,
    QueryTags.PortfolioStrategicInitiatives,
  ],
  'strategic.themes': [QueryTags.StrategicTheme],
  'product-management.products': [{ type: QueryTags.Product, id: 'LIST' }],
  'product-management.versions': [{ type: QueryTags.Version, id: 'LIST' }],
  'product-management.releases': [{ type: QueryTags.Release, id: 'LIST' }],
  'product-management.release-packages': [
    { type: QueryTags.ReleasePackage, id: 'LIST' },
  ],
  'product-management.deployment-environments': [
    { type: QueryTags.DeploymentEnvironment, id: 'LIST' },
  ],
  'product-management.deployments': [
    { type: QueryTags.Deployment, id: 'LIST' },
    { type: QueryTags.DeliveryMetrics, id: 'LIST' },
  ],
}

export const importedRecordTags = (importType: string) =>
  IMPORTED_RECORD_TAGS[importType as ImportKey] ?? []

// The generated client takes a FileParameter, so the browser File travels with its name.
const upload = (file: File | undefined) =>
  file ? { data: file, fileName: file.name } : undefined

/**
 * Posts each import's files to its endpoint through the generated client. A file uploaded from the app
 * is submitted on its own, under no group.
 *
 * Keyed by every generated import for the same reason as the tags. A required file is guaranteed by the
 * form, which will not submit without one.
 */
const SUBMITTERS: Record<
  ImportKey,
  (files: SubmitImportRequest['files']) => Promise<ImportProcessDto>
> = {
  employees: ({ file }) => getEmployeesClient().import(undefined, upload(file)),
  teams: ({ file }) => getTeamsClient().import(undefined, upload(file)),
  'team-members': ({ file }) =>
    getTeamsClient().importMembers(undefined, upload(file)),
  'team-memberships': ({ file }) =>
    getTeamsClient().importTeamMemberships(undefined, upload(file)),
  'planning.planning-intervals': ({ file }) =>
    getPlanningIntervalsClient().import(undefined, upload(file)),
  'planning.planning-interval-objectives': ({ file }) =>
    getPlanningIntervalsClient().importObjectives(undefined, upload(file)),
  'planning.risks': ({ file }) =>
    getRisksClient().import(undefined, upload(file)),
  'ppm.portfolios': ({ file }) =>
    getPortfoliosClient().import(undefined, upload(file)),
  'ppm.programs': ({ file }) =>
    getProgramsClient().import(undefined, upload(file)),
  'ppm.projects': ({ file }) =>
    getProjectsClient().import(undefined, upload(file)),
  'ppm.project-tasks': ({ file }) =>
    getProjectsClient().importTasks(undefined, upload(file)),
  'ppm.project-stages': ({ file }) =>
    getProjectsClient().importStages(undefined, upload(file)),
  'ppm.finalizations': ({ file }) =>
    getPortfoliosClient().finalizeImport(undefined, upload(file)),
  'ppm.strategic-initiatives': ({ file, kpiFile }) =>
    getStrategicInitiativesClient().import(
      undefined,
      upload(file),
      upload(kpiFile),
    ),
  'strategic.themes': ({ file }) =>
    getStrategicThemesClient().import(undefined, upload(file)),
  'product-management.products': ({ file }) =>
    getProductsClient().import(undefined, upload(file)),
  'product-management.versions': ({ file }) =>
    getVersionsClient().import(undefined, upload(file)),
  'product-management.releases': ({ file, contentsFile }) =>
    getReleasesClient().import(undefined, upload(file), upload(contentsFile)),
  'product-management.release-packages': ({ file, manifestFile }) =>
    getReleasePackagesClient().import(
      undefined,
      upload(file),
      upload(manifestFile),
    ),
  'product-management.deployment-environments': ({ file }) =>
    getDeploymentEnvironmentsClient().import(undefined, upload(file)),
  'product-management.deployments': ({ file }) =>
    getDeploymentsClient().import(undefined, upload(file)),
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
    submitImport: builder.mutation<ImportProcessDto, SubmitImportRequest>({
      queryFn: async ({ importKey, files }) => {
        try {
          const data = await SUBMITTERS[importKey](files)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A refused file creates no run and changes no record, so there is nothing to refetch. The records
      // only include the rows if the run finished within the wait; one still running refreshes them from
      // its import page when it lands.
      invalidatesTags: (result, error, { importKey }) =>
        result
          ? [QueryTags.ImportProcess, ...IMPORTED_RECORD_TAGS[importKey]]
          : [],
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
  useSubmitImportMutation,
  useCancelImportProcessMutation,
  useResumeImportProcessMutation,
  useRetryFailedImportRowsMutation,
} = importsApi
