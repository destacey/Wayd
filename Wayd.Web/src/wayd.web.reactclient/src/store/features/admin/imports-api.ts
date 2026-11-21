import {
  ImportDefinitionDto,
  ImportProcessDto,
  ImportProcessStatus,
  PagedResponseOfImportProcessDto,
  ResumedImport,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { deploymentEnvironmentsImport } from '@/src/services/api/deployment-environments'
import { deploymentsImport } from '@/src/services/api/deployments'
import { employeesImport } from '@/src/services/api/employees'
import {
  importsApply,
  importsCancel,
  importsGetById,
  importsGetDefinitions,
  importsGetList,
  importsResume,
  importsRetryFailed,
} from '@/src/services/api/imports'
import { incidentsImport } from '@/src/services/api/incidents'
import {
  intakeRequestsImport,
  intakeRequestsImportOutcomes,
} from '@/src/services/api/intake-requests'
import {
  planningIntervalsImport,
  planningIntervalsImportObjectives,
} from '@/src/services/api/planning-intervals'
import {
  portfoliosFinalizeImport,
  portfoliosImport,
} from '@/src/services/api/portfolios'
import {
  productsImport,
  productsImportDependencies,
} from '@/src/services/api/products'
import { programsImport } from '@/src/services/api/programs'
import { projectDeliverablesImportDeliverables } from '@/src/services/api/project-deliverables'
import {
  projectsImport,
  projectsImportStages,
  projectsImportTasks,
} from '@/src/services/api/projects'
import { kpiImportsImport } from '@/src/services/api/kpi-imports'
import { releasePackagesImport } from '@/src/services/api/release-packages'
import { releasesImport } from '@/src/services/api/releases'
import { risksImport } from '@/src/services/api/risks'
import { strategicInitiativesImport } from '@/src/services/api/strategic-initiatives'
import { strategicThemesImport } from '@/src/services/api/strategic-themes'
import {
  teamsImport,
  teamsImportMembers,
  teamsImportTeamMemberships,
} from '@/src/services/api/teams'
import { versionsImport } from '@/src/services/api/versions'
import type { ImportKey } from '@/src/components/common/import/import-templates.generated'
import { isApiError } from '@/src/utils'

export interface GetImportProcessesRequest {
  status?: ImportProcessStatus
  importType?: string
  submittedByUserId?: string
  /** Only the runs of one batch — the files a caller posted together. */
  submissionGroupId?: string | null
  /** 1-based. */
  page?: number
  pageSize?: number
}

export interface SubmitImportRequest {
  importKey: ImportKey
  /** The chosen files, keyed by the multipart field each is posted as. */
  files: Partial<Record<string, File>>
  /** Checks every row as the import would and applies none of them. */
  validateOnly?: boolean
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
    // Imported tasks can serve deliverables, whose progress they move.
    QueryTags.ProjectDeliverable,
  ],
  'ppm.project-stages': [QueryTags.ProjectPlanTree, QueryTags.ProjectTaskTree],
  'ppm.project-deliverables': [QueryTags.ProjectDeliverable, QueryTags.Project],
  'ppm.finalizations': [QueryTags.Portfolio, QueryTags.Program],
  'ppm.strategic-initiatives': [
    QueryTags.StrategicInitiative,
    QueryTags.Kpi,
    QueryTags.PortfolioStrategicInitiatives,
  ],
  'strategic.themes': [QueryTags.StrategicTheme],
  'product-management.products': [{ type: QueryTags.Product, id: 'LIST' }],
  'product-management.product-dependencies': [
    { type: QueryTags.ProductDependency, id: 'LIST' },
  ],
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
  'product-management.incidents': [{ type: QueryTags.Incident, id: 'LIST' }],
  'demand-management.intake-requests': [
    { type: QueryTags.IntakeRequest, id: 'LIST' },
    QueryTags.IntakeRequestScore,
  ],
  // Every scorecard and KPI drawer: the rows name records across the organization.
  // The categories list counts the KPIs in each category.
  'kpis.kpis': [
    QueryTags.KpiScorecard,
    QueryTags.Kpi,
    { type: QueryTags.KpiCategory, id: 'LIST' },
  ],
  // Outcomes show on each request and on the project and work item lists, which all share this type.
  'demand-management.intake-request-outcomes': [QueryTags.IntakeRequest],
}

export const importedRecordTags = (importType: string) =>
  IMPORTED_RECORD_TAGS[importType as ImportKey] ?? []

// The form will not submit without an import's required file, so this fails
// only if that ever changes, rather than posting the text "undefined" as the file.
const required = (file: File | undefined): File => {
  if (!file) throw new Error('Choose a file to import.')
  return file
}

/**
 * Posts each import's files to its endpoint through the generated client. A file uploaded from the app
 * is submitted on its own, under no group.
 *
 * Keyed by every generated import for the same reason as the tags. A required file is guaranteed by the
 * form, which will not submit without one.
 */
const SUBMITTERS: Record<
  ImportKey,
  (
    files: SubmitImportRequest['files'],
    validateOnly: boolean,
  ) => Promise<ImportProcessDto>
> = {
  employees: ({ file }, validateOnly) =>
    employeesImport({ file: required(file) }, { validateOnly: validateOnly }),
  teams: ({ file }, validateOnly) =>
    teamsImport({ file: required(file) }, { validateOnly: validateOnly }),
  'team-members': ({ file }, validateOnly) =>
    teamsImportMembers(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'team-memberships': ({ file }, validateOnly) =>
    teamsImportTeamMemberships(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'planning.planning-intervals': ({ file }, validateOnly) =>
    planningIntervalsImport(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'planning.planning-interval-objectives': ({ file }, validateOnly) =>
    planningIntervalsImportObjectives(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'planning.risks': ({ file }, validateOnly) =>
    risksImport({ file: required(file) }, { validateOnly: validateOnly }),
  'ppm.portfolios': ({ file }, validateOnly) =>
    portfoliosImport({ file: required(file) }, { validateOnly: validateOnly }),
  'ppm.programs': ({ file }, validateOnly) =>
    programsImport({ file: required(file) }, { validateOnly: validateOnly }),
  'ppm.projects': ({ file }, validateOnly) =>
    projectsImport({ file: required(file) }, { validateOnly: validateOnly }),
  'ppm.project-tasks': ({ file }, validateOnly) =>
    projectsImportTasks(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'ppm.project-stages': ({ file }, validateOnly) =>
    projectsImportStages(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'ppm.project-deliverables': ({ file }, validateOnly) =>
    projectDeliverablesImportDeliverables(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'ppm.finalizations': ({ file }, validateOnly) =>
    portfoliosFinalizeImport(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'ppm.strategic-initiatives': ({ file, kpiFile }, validateOnly) =>
    strategicInitiativesImport(
      { file: required(file), kpiFile: kpiFile },
      { validateOnly: validateOnly },
    ),
  'strategic.themes': ({ file }, validateOnly) =>
    strategicThemesImport(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'product-management.products': ({ file }, validateOnly) =>
    productsImport({ file: required(file) }, { validateOnly: validateOnly }),
  'product-management.product-dependencies': ({ file }, validateOnly) =>
    productsImportDependencies(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'product-management.versions': ({ file }, validateOnly) =>
    versionsImport({ file: required(file) }, { validateOnly: validateOnly }),
  'product-management.releases': ({ file, contentsFile }, validateOnly) =>
    releasesImport(
      { file: required(file), contentsFile: contentsFile },
      { validateOnly: validateOnly },
    ),
  'product-management.release-packages': (
    { file, manifestFile },
    validateOnly,
  ) =>
    releasePackagesImport(
      { file: required(file), manifestFile: required(manifestFile) },
      { validateOnly: validateOnly },
    ),
  'product-management.deployment-environments': ({ file }, validateOnly) =>
    deploymentEnvironmentsImport(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'product-management.deployments': ({ file }, validateOnly) =>
    deploymentsImport({ file: required(file) }, { validateOnly: validateOnly }),
  'product-management.incidents': ({ file }, validateOnly) =>
    incidentsImport({ file: required(file) }, { validateOnly: validateOnly }),
  'kpis.kpis': ({ file, measurementsFile, checkpointsFile }, validateOnly) =>
    kpiImportsImport(
      {
        file: required(file),
        measurementsFile: required(measurementsFile),
        checkpointsFile: checkpointsFile,
      },
      { validateOnly: validateOnly },
    ),
  'demand-management.intake-requests': ({ file }, validateOnly) =>
    intakeRequestsImport(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
  'demand-management.intake-request-outcomes': ({ file }, validateOnly) =>
    intakeRequestsImportOutcomes(
      { file: required(file) },
      { validateOnly: validateOnly },
    ),
}

export const importsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getImportProcesses: builder.query<
      PagedResponseOfImportProcessDto,
      GetImportProcessesRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await importsGetList({
            status: request.status,
            importType: request.importType,
            submittedByUserId: request.submittedByUserId,
            submissionGroupId: request.submissionGroupId ?? undefined,
            page: request.page,
            pageSize: request.pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.ImportProcess,
        ...(result?.items?.map(({ id }) => ({
          type: QueryTags.ImportProcess,
          id,
        })) ?? []),
      ],
    }),
    getImportProcessById: builder.query<ImportProcessDto, string>({
      queryFn: async (id) => {
        try {
          const data = await importsGetById(id)
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
    getImportDefinitions: builder.query<ImportDefinitionDto[], void>({
      queryFn: async () => {
        try {
          const data = await importsGetDefinitions()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.ImportDefinition],
    }),
    submitImport: builder.mutation<ImportProcessDto, SubmitImportRequest>({
      queryFn: async ({ importKey, files, validateOnly = false }) => {
        try {
          const data = await SUBMITTERS[importKey](files, validateOnly)
          return { data }
        } catch (error) {
          // A refused file is an outcome the dialog shows, not a failure to log; the client
          // interceptor has already noted the request.
          if (
            !isApiError(error) ||
            (error.status !== 400 && error.status !== 422)
          ) {
            console.error('API Error:', error)
          }
          return { error }
        }
      },
      // A refused file creates no run and changes no record, so there is nothing to refetch. The records
      // only include the rows if the run finished within the wait; one still running refreshes them from
      // its import page when it lands. A preflight changes no record at all.
      invalidatesTags: (result, error, { importKey }) =>
        !result
          ? []
          : result.isPreflight
            ? [QueryTags.ImportProcess]
            : [QueryTags.ImportProcess, ...IMPORTED_RECORD_TAGS[importKey]],
    }),
    applyImportPreflight: builder.mutation<ImportProcessDto, string>({
      queryFn: async (id) => {
        try {
          const data = await importsApply(id)
          return { data }
        } catch (error) {
          if (!isApiError(error) || error.status !== 400) {
            console.error('API Error:', error)
          }
          return { error }
        }
      },
      // The new run's records only exist if it finished within the wait; its page refreshes them otherwise.
      invalidatesTags: (result) =>
        result
          ? [QueryTags.ImportProcess, ...importedRecordTags(result.importType)]
          : [],
    }),
    cancelImportProcess: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await importsCancel(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        QueryTags.ImportProcess,
        { type: QueryTags.ImportProcess, id },
      ],
    }),
    resumeImportProcess: builder.mutation<ResumedImport, string>({
      queryFn: async (id) => {
        try {
          const data = await importsResume(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        QueryTags.ImportProcess,
        { type: QueryTags.ImportProcess, id },
      ],
    }),
    retryFailedImportRows: builder.mutation<ResumedImport, string>({
      queryFn: async (id) => {
        try {
          const data = await importsRetryFailed(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        QueryTags.ImportProcess,
        { type: QueryTags.ImportProcess, id },
      ],
    }),
  }),
})

export const {
  useGetImportProcessesQuery,
  useGetImportProcessByIdQuery,
  useGetImportDefinitionsQuery,
  useSubmitImportMutation,
  useApplyImportPreflightMutation,
  useCancelImportProcessMutation,
  useResumeImportProcessMutation,
  useRetryFailedImportRowsMutation,
} = importsApi
