import {
  CreatePlanningIntervalObjectiveRequest,
  CreatePlanningIntervalRequest,
  ManagePlanningIntervalDatesRequest,
  ManagePlanningIntervalObjectiveWorkItemsRequest,
  MapPlanningIntervalSprintsRequest,
  ObjectIdAndKey,
  PagedResponseOfActivityLogDto,
  PlanningIntervalCalendarDto,
  PlanningIntervalDetailsDto,
  PlanningIntervalIterationDetailsDto,
  PlanningIntervalIterationListDto,
  PlanningIntervalIterationMetricsResponse,
  PlanningIntervalIterationSprintsDto,
  PlanningIntervalListDto,
  PlanningIntervalMetricsResponse,
  PlanningIntervalObjectiveDetailsDto,
  PlanningIntervalObjectiveHealthCheckDto,
  PlanningIntervalObjectiveListDto,
  PlanningIntervalObjectiveStatusDto,
  PlanningIntervalPredictabilityDto,
  PlanningIntervalTeamResponse,
  RiskListDto,
  SprintBacklogItemDto,
  DependencyDto,
  UpdatePlanningIntervalObjectiveRequest,
  UpdatePlanningIntervalObjectivesOrderRequest,
  UpdatePlanningIntervalRequest,
  WorkItemProgressDailyRollupDto,
  WorkItemsSummaryDto,
  PlanningIntervalCapacityDto,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import {
  planningIntervalsCreate,
  planningIntervalsCreateObjective,
  planningIntervalsDeleteObjective,
  planningIntervalsGetActivities,
  planningIntervalsGetBacklog,
  planningIntervalsGetCalendar,
  planningIntervalsGetCapacity,
  planningIntervalsGetDependencies,
  planningIntervalsGetIteration,
  planningIntervalsGetIterationBacklog,
  planningIntervalsGetIterationCategories,
  planningIntervalsGetIterationMetrics,
  planningIntervalsGetIterationSprints,
  planningIntervalsGetIterations,
  planningIntervalsGetList,
  planningIntervalsGetMetrics,
  planningIntervalsGetObjective,
  planningIntervalsGetObjectiveActivities,
  planningIntervalsGetObjectiveStatuses,
  planningIntervalsGetObjectiveWorkItemMetrics,
  planningIntervalsGetObjectiveWorkItems,
  planningIntervalsGetObjectives,
  planningIntervalsGetObjectivesHealthReport,
  planningIntervalsGetPlanningInterval,
  planningIntervalsGetPredictability,
  planningIntervalsGetRisks,
  planningIntervalsGetTeamPredictability,
  planningIntervalsGetTeams,
  planningIntervalsManageDates,
  planningIntervalsManageObjectiveWorkItems,
  planningIntervalsMapTeamSprints,
  planningIntervalsUpdate,
  planningIntervalsUpdateObjective,
  planningIntervalsUpdateObjectivesOrder,
} from '@/src/services/api/planning-intervals'
import { QueryTags } from '../query-tags'
import { OptionModel } from '@/src/components/types'

// Every cached view of a sprint, whose type a mapping or an iteration's
// category decides. By type: these mutations aren't given the sprints.
const sprintTypeTags = [
  QueryTags.Sprint,
  QueryTags.TeamSprint,
  QueryTags.ActiveSprint,
] as const

export const planningIntervalApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPlanningIntervals: builder.query<PlanningIntervalListDto[], void>({
      queryFn: async () => {
        try {
          const data = await planningIntervalsGetList()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.PlanningInterval, id: 'LIST' }],
    }),
    getPlanningInterval: builder.query<PlanningIntervalDetailsDto, number>({
      queryFn: async (idOrKey) => {
        try {
          const data = await planningIntervalsGetPlanningInterval(
            idOrKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningInterval, id: arg }, // typically arg is the key
      ],
    }),
    createPlanningInterval: builder.mutation<
      ObjectIdAndKey,
      CreatePlanningIntervalRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [{ type: QueryTags.PlanningInterval, id: 'LIST' }]
      },
    }),
    updatePlanningInterval: builder.mutation<
      void,
      { request: UpdatePlanningIntervalRequest; cacheKey: number }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await planningIntervalsUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.PlanningInterval, id: 'LIST' },
          { type: QueryTags.PlanningInterval, id: arg.cacheKey },
          { type: QueryTags.ActivityLog, id: arg.request.id },
        ]
      },
    }),
    updatePlanningIntervalDates: builder.mutation<
      void,
      { request: ManagePlanningIntervalDatesRequest; cacheKey: number }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await planningIntervalsManageDates(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.PlanningInterval, id: 'LIST' },
          { type: QueryTags.PlanningInterval, id: arg.cacheKey },
          { type: QueryTags.PlanningIntervalCalendar, id: arg.cacheKey },
          { type: QueryTags.PlanningIntervalIteration, id: arg.cacheKey },
          { type: QueryTags.ActivityLog, id: arg.request.id },
          // A mapped sprint's type follows its iteration's category, and
          // removing an iteration unmaps its sprints.
          ...sprintTypeTags,
        ]
      },
    }),
    getPlanningIntervalCalendar: builder.query<
      PlanningIntervalCalendarDto,
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await planningIntervalsGetCalendar(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningIntervalCalendar, id: arg }, // typically arg is the key
      ],
    }),
    getPlanningIntervalPredictability: builder.query<
      PlanningIntervalPredictabilityDto,
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await planningIntervalsGetPredictability(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningIntervalPredictability, id: arg }, // typically arg is the key
      ],
    }),
    getPlanningIntervalCapacity: builder.query<
      PlanningIntervalCapacityDto,
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await planningIntervalsGetCapacity(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Read from the PI's dates, iterations and teams, and from the teams'
      // allocations, time away and sprint days off, whose changes invalidate
      // the PI's tag or every team capacity tag.
      providesTags: (result, error, key) => [
        { type: QueryTags.PlanningInterval, id: key },
        { type: QueryTags.TeamCapacity, id: `planning-interval-${key}` },
      ],
    }),

    getPlanningIntervalMetrics: builder.query<
      PlanningIntervalMetricsResponse,
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await planningIntervalsGetMetrics(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningIntervalMetrics, id: arg },
      ],
    }),
    getPlanningIntervalIterations: builder.query<
      PlanningIntervalIterationListDto[],
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await planningIntervalsGetIterations(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningIntervalIteration, id: arg }, // typically arg is the key
      ],
    }),
    getPlanningIntervalIteration: builder.query<
      PlanningIntervalIterationDetailsDto,
      { planningIntervalKey: number; iterationKey: number }
    >({
      queryFn: async ({ planningIntervalKey, iterationKey }) => {
        try {
          const data = await planningIntervalsGetIteration(
            planningIntervalKey.toString(),
            iterationKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalIteration,
          id: `${arg.planningIntervalKey}:${arg.iterationKey}`,
        },
      ],
    }),
    getIterationSprints: builder.query<
      PlanningIntervalIterationSprintsDto[],
      { idOrKey: string; iterationId?: string }
    >({
      queryFn: async ({ idOrKey, iterationId }) => {
        try {
          const data = await planningIntervalsGetIterationSprints(idOrKey, {
            iterationId: iterationId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalIterationSprints,
          id: arg.iterationId
            ? `${arg.idOrKey}:${arg.iterationId}`
            : arg.idOrKey,
        },
      ],
    }),
    getPlanningIntervalIterationMetrics: builder.query<
      PlanningIntervalIterationMetricsResponse,
      { planningIntervalKey: number; iterationKey: number }
    >({
      queryFn: async ({ planningIntervalKey, iterationKey }) => {
        try {
          const data = await planningIntervalsGetIterationMetrics(
            planningIntervalKey.toString(),
            iterationKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalIterationMetrics,
          id: `${arg.planningIntervalKey}:${arg.iterationKey}`,
        },
      ],
    }),
    getPlanningIntervalBacklog: builder.query<SprintBacklogItemDto[], number>({
      queryFn: async (planningIntervalKey) => {
        try {
          const data = await planningIntervalsGetBacklog(
            planningIntervalKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningIntervalBacklog, id: arg },
      ],
    }),
    getPlanningIntervalDependencies: builder.query<DependencyDto[], number>({
      queryFn: async (planningIntervalKey) => {
        try {
          const data = await planningIntervalsGetDependencies(
            planningIntervalKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.PlanningIntervalDependencies,
        { type: QueryTags.PlanningIntervalDependencies, id: arg },
      ],
    }),
    getPlanningIntervalIterationBacklog: builder.query<
      SprintBacklogItemDto[],
      { planningIntervalKey: number; iterationKey: number }
    >({
      queryFn: async ({ planningIntervalKey, iterationKey }) => {
        try {
          const data = await planningIntervalsGetIterationBacklog(
            planningIntervalKey.toString(),
            iterationKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalIterationBacklog,
          id: `${arg.planningIntervalKey}:${arg.iterationKey}`,
        },
      ],
    }),
    getPlanningIntervalIterationCategoryOptions: builder.query<
      OptionModel<number>[],
      void
    >({
      queryFn: async () => {
        try {
          const categories = await planningIntervalsGetIterationCategories()

          const data: OptionModel<number>[] = categories
            .sort((a, b) => a.order - b.order)
            .map((t) => ({
              label: t.name,
              value: t.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },

      providesTags: () => [
        { type: QueryTags.PlanningIntervalIterationTypeOptions, id: 'LIST' },
      ],
    }),
    getPlanningIntervalTeams: builder.query<
      PlanningIntervalTeamResponse[],
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await planningIntervalsGetTeams(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningIntervalTeam, id: arg }, // typically arg is the key
      ],
    }),
    getPlanningIntervalTeamPredictability: builder.query<
      number | null,
      { planningIntervalKey: number; teamId: string }
    >({
      queryFn: async ({ planningIntervalKey, teamId }) => {
        try {
          const data = await planningIntervalsGetTeamPredictability(
            planningIntervalKey.toString(),
            teamId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalTeamPredictability,
          id: `${arg.planningIntervalKey}:${arg.teamId}`,
        },
      ],
    }),
    mapTeamSprints: builder.mutation<
      void,
      {
        planningIntervalId: string
        teamId: string
        request: MapPlanningIntervalSprintsRequest
        cacheKey: number
      }
    >({
      queryFn: async ({ planningIntervalId, teamId, request }) => {
        try {
          const data = await planningIntervalsMapTeamSprints(
            planningIntervalId,
            teamId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.PlanningInterval, id: arg.cacheKey },
        { type: QueryTags.PlanningIntervalIteration, id: arg.cacheKey },
        { type: QueryTags.PlanningIntervalTeam, id: arg.cacheKey },
        { type: QueryTags.PlanningIntervalIterationSprints, id: arg.cacheKey },
        { type: QueryTags.ActivityLog, id: arg.planningIntervalId },
        // A mapped sprint's type follows its iteration's category.
        ...sprintTypeTags,
      ],
    }),
    getPlanningIntervalObjectives: builder.query<
      PlanningIntervalObjectiveListDto[],
      {
        planningIntervalKey: number
        teamId?: string
      }
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsGetObjectives(
            request.planningIntervalKey.toString(),
            { teamId: request.teamId },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        { type: QueryTags.PlanningIntervalObjective, id: 'LIST' },
        ...(result ?? []).map(({ key }) => ({
          type: QueryTags.PlanningIntervalObjective,
          id: key,
        })),
      ],
    }),
    getPlanningIntervalObjective: builder.query<
      PlanningIntervalObjectiveDetailsDto,
      { planningIntervalKey: string; objectiveKey: string }
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsGetObjective(
            request.planningIntervalKey,
            request.objectiveKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PlanningIntervalObjective, id: arg.objectiveKey },
      ],
    }),
    createPlanningIntervalObjective: builder.mutation<
      ObjectIdAndKey,
      {
        request: CreatePlanningIntervalObjectiveRequest
        planningIntervalKey: number
      }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await planningIntervalsCreateObjective(
            request.planningIntervalId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.PlanningIntervalObjective, id: 'LIST' },
          {
            type: QueryTags.PlanningIntervalPredictability,
            id: arg.planningIntervalKey,
          },
          {
            type: QueryTags.PlanningIntervalTeamPredictability,
            id: `${arg.planningIntervalKey}:${arg.request.teamId}`,
          },
          ...(result ? [{ type: QueryTags.ActivityLog, id: result.id }] : []),
        ]
      },
    }),
    updatePlanningIntervalObjective: builder.mutation<
      void,
      {
        request: UpdatePlanningIntervalObjectiveRequest
        objectiveKey: number
        planningIntervalKey: number
        teamId: string
      }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await planningIntervalsUpdateObjective(
            request.planningIntervalId,
            request.objectiveId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.PlanningIntervalObjective, id: 'LIST' },
          { type: QueryTags.PlanningIntervalObjective, id: arg.objectiveKey },
          {
            type: QueryTags.PlanningIntervalPredictability,
            id: arg.planningIntervalKey,
          },
          {
            type: QueryTags.PlanningIntervalTeamPredictability,
            id: `${arg.planningIntervalKey}:${arg.teamId}`,
          },
          { type: QueryTags.ActivityLog, id: arg.request.objectiveId },
        ]
      },
    }),
    updateObjectivesOrder: builder.mutation<
      void,
      UpdatePlanningIntervalObjectivesOrderRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsUpdateObjectivesOrder(
            request.planningIntervalId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.PlanningIntervalObjective },
          ...Object.keys(arg.objectives).map((objectiveId) => ({
            type: QueryTags.ActivityLog,
            id: objectiveId,
          })),
        ]
      },
    }),
    deletePlanningIntervalObjective: builder.mutation<
      void,
      {
        planningIntervalId: string
        planningIntervalKey: number
        objectiveId: string
        objectiveKey: number
        teamId: string
      }
    >({
      queryFn: async ({ planningIntervalId, objectiveId }) => {
        try {
          const data = await planningIntervalsDeleteObjective(
            planningIntervalId,
            objectiveId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.PlanningIntervalObjective, id: 'LIST' },
          { type: QueryTags.PlanningIntervalObjective, id: arg.objectiveKey },
          {
            type: QueryTags.PlanningIntervalPredictability,
            id: arg.planningIntervalKey,
          },
          {
            type: QueryTags.PlanningIntervalTeamPredictability,
            id: `${arg.planningIntervalKey}:${arg.teamId}`,
          },
          { type: QueryTags.ActivityLog, id: arg.objectiveId },
        ]
      },
    }),
    getObjectiveWorkItems: builder.query<
      WorkItemsSummaryDto,
      {
        planningIntervalKey: string
        objectiveKey: string
      }
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsGetObjectiveWorkItems(
            request.planningIntervalKey,
            request.objectiveKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalObjectiveWorkItemsSummary,
          id: arg.objectiveKey,
        },
      ],
    }),
    getObjectiveWorkItemMetrics: builder.query<
      WorkItemProgressDailyRollupDto[],
      {
        planningIntervalKey: string
        objectiveKey: string
      }
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsGetObjectiveWorkItemMetrics(
            request.planningIntervalKey,
            request.objectiveKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalObjectiveWorkItemMetric,
          id: arg.objectiveKey,
        },
      ],
    }),
    manageObjectiveWorkItems: builder.mutation<
      void,
      {
        request: ManagePlanningIntervalObjectiveWorkItemsRequest
        cacheKey: string
      }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await planningIntervalsManageObjectiveWorkItems(
            request.planningIntervalId,
            request.objectiveId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          {
            type: QueryTags.PlanningIntervalObjectiveWorkItemsSummary,
            id: arg.cacheKey,
          },
        ]
      },
    }),
    getPlanningIntervalObjectivesHealthReport: builder.query<
      PlanningIntervalObjectiveHealthCheckDto[],
      { planningIntervalKey: number; teamId?: string }
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsGetObjectivesHealthReport(
            request.planningIntervalKey.toString(),
            { teamId: request.teamId },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
    getPlanningIntervalObjectiveStatuses: builder.query<
      PlanningIntervalObjectiveStatusDto[],
      void
    >({
      queryFn: async () => {
        try {
          const data = await planningIntervalsGetObjectiveStatuses()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalObjectiveStatus,
          id: 'LIST',
        },
      ],
    }),
    getPlanningIntervalObjectiveStatusOptions: builder.query<
      OptionModel<number>[],
      void
    >({
      queryFn: async () => {
        try {
          const types = await planningIntervalsGetObjectiveStatuses()

          const data: OptionModel<number>[] = types
            .sort((a, b) => a.order - b.order)
            .map((t) => ({
              label: t.name,
              value: t.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },

      providesTags: () => [
        { type: QueryTags.PlanningIntervalObjectiveStatusOptions, id: 'LIST' },
      ],
    }),
    getPlanningIntervalRisks: builder.query<
      RiskListDto[],
      {
        planningIntervalKey: number
        includeClosed?: boolean
        teamId?: string
      }
    >({
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsGetRisks(
            request.planningIntervalKey.toString(),
            { includeClosed: request.includeClosed, teamId: request.teamId },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.PlanningIntervalRisk,
          id: arg.teamId
            ? `${arg.planningIntervalKey}:${arg.teamId}`
            : arg.planningIntervalKey,
        },
      ],
    }),
    getPlanningIntervalActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await planningIntervalsGetActivities(String(idOrKey), {
            page: page,
            pageSize: pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),
    getPlanningIntervalObjectiveActivities: builder.query<
      PagedResponseOfActivityLogDto,
      {
        planningIntervalIdOrKey: string | number
        objectiveIdOrKey: string | number
        page?: number
        pageSize?: number
      }
    >({
      queryFn: async ({
        planningIntervalIdOrKey,
        objectiveIdOrKey,
        page,
        pageSize,
      }) => {
        try {
          const data = await planningIntervalsGetObjectiveActivities(
            String(planningIntervalIdOrKey),
            String(objectiveIdOrKey),
            { page: page, pageSize: pageSize },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { objectiveIdOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(objectiveIdOrKey) },
      ],
    }),
  }),
})

export const {
  useGetPlanningIntervalsQuery,
  useGetPlanningIntervalQuery,
  useCreatePlanningIntervalMutation,
  useUpdatePlanningIntervalMutation,
  useUpdatePlanningIntervalDatesMutation,
  useGetPlanningIntervalCalendarQuery,
  useGetPlanningIntervalPredictabilityQuery,
  useGetPlanningIntervalMetricsQuery,
  useGetPlanningIntervalCapacityQuery,
  useGetPlanningIntervalIterationsQuery,
  useGetPlanningIntervalIterationQuery,
  useGetPlanningIntervalIterationMetricsQuery,
  useGetPlanningIntervalBacklogQuery,
  useGetPlanningIntervalDependenciesQuery,
  useGetPlanningIntervalIterationBacklogQuery,
  useGetIterationSprintsQuery,
  useGetPlanningIntervalIterationCategoryOptionsQuery,
  useGetPlanningIntervalTeamsQuery,
  useGetPlanningIntervalTeamPredictabilityQuery,
  useMapTeamSprintsMutation,
  useGetPlanningIntervalObjectivesQuery,
  useGetPlanningIntervalObjectiveQuery,
  useCreatePlanningIntervalObjectiveMutation,
  useUpdatePlanningIntervalObjectiveMutation,
  useUpdateObjectivesOrderMutation,
  useDeletePlanningIntervalObjectiveMutation,
  useGetObjectiveWorkItemsQuery,
  useGetObjectiveWorkItemMetricsQuery,
  useManageObjectiveWorkItemsMutation,
  useGetPlanningIntervalObjectivesHealthReportQuery,
  useGetPlanningIntervalObjectiveStatusesQuery,
  useGetPlanningIntervalObjectiveStatusOptionsQuery,
  useGetPlanningIntervalRisksQuery,
  useGetPlanningIntervalActivitiesQuery,
  useLazyGetPlanningIntervalActivitiesQuery,
  useGetPlanningIntervalObjectiveActivitiesQuery,
  useLazyGetPlanningIntervalObjectiveActivitiesQuery,
} = planningIntervalApi
