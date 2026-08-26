import {
  CreatePlanningIntervalObjectiveRequest,
  CreatePlanningIntervalRequest,
  ManagePlanningIntervalDatesRequest,
  ManagePlanningIntervalObjectiveWorkItemsRequest,
  MapPlanningIntervalSprintsRequest,
  ObjectIdAndKey,
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
  UpdatePlanningIntervalObjectiveRequest,
  UpdatePlanningIntervalObjectivesOrderRequest,
  UpdatePlanningIntervalRequest,
  WorkItemProgressDailyRollupDto,
  WorkItemsSummaryDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { getPlanningIntervalsClient } from '@/src/services/clients'
import { QueryTags } from '../query-tags'
import { OptionModel } from '@/src/components/types'

export const planningIntervalApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPlanningIntervals: builder.query<PlanningIntervalListDto[], void>({
      queryFn: async () => {
        try {
          const data = await getPlanningIntervalsClient().getList()
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
          const data = await getPlanningIntervalsClient().getPlanningInterval(
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
          const data = await getPlanningIntervalsClient().create(request)
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
          const data = await getPlanningIntervalsClient().update(
            request.id,
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
          { type: QueryTags.PlanningInterval, id: 'LIST' },
          { type: QueryTags.PlanningInterval, id: arg.cacheKey },
        ]
      },
    }),
    updatePlanningIntervalDates: builder.mutation<
      void,
      { request: ManagePlanningIntervalDatesRequest; cacheKey: number }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await getPlanningIntervalsClient().manageDates(
            request.id,
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
          { type: QueryTags.PlanningInterval, id: 'LIST' },
          { type: QueryTags.PlanningInterval, id: arg.cacheKey },
          { type: QueryTags.PlanningIntervalCalendar, id: arg.cacheKey },
          { type: QueryTags.PlanningIntervalIteration, id: arg.cacheKey },
        ]
      },
    }),
    getPlanningIntervalCalendar: builder.query<
      PlanningIntervalCalendarDto,
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await getPlanningIntervalsClient().getCalendar(
            key.toString(),
          )
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
          const data = await getPlanningIntervalsClient().getPredictability(
            key.toString(),
          )
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
    getPlanningIntervalMetrics: builder.query<
      PlanningIntervalMetricsResponse,
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await getPlanningIntervalsClient().getMetrics(
            key.toString(),
          )
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
          const data = await getPlanningIntervalsClient().getIterations(
            key.toString(),
          )
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
          const data = await getPlanningIntervalsClient().getIteration(
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
          const data = await getPlanningIntervalsClient().getIterationSprints(
            idOrKey,
            iterationId,
          )
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
          const data = await getPlanningIntervalsClient().getIterationMetrics(
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
          const data = await getPlanningIntervalsClient().getBacklog(
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
    getPlanningIntervalIterationBacklog: builder.query<
      SprintBacklogItemDto[],
      { planningIntervalKey: number; iterationKey: number }
    >({
      queryFn: async ({ planningIntervalKey, iterationKey }) => {
        try {
          const data = await getPlanningIntervalsClient().getIterationBacklog(
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
          const categories =
            await getPlanningIntervalsClient().getIterationCategories()

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
          const data = await getPlanningIntervalsClient().getTeams(
            key.toString(),
          )
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
          const data = await getPlanningIntervalsClient().getTeamPredictability(
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
          const data = await getPlanningIntervalsClient().mapTeamSprints(
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
          const data = await getPlanningIntervalsClient().getObjectives(
            request.planningIntervalKey.toString(),
            request.teamId,
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
          const data = await getPlanningIntervalsClient().getObjective(
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
          const data = await getPlanningIntervalsClient().createObjective(
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
          const data = await getPlanningIntervalsClient().updateObjective(
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
        ]
      },
    }),
    updateObjectivesOrder: builder.mutation<
      void,
      UpdatePlanningIntervalObjectivesOrderRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getPlanningIntervalsClient().updateObjectivesOrder(
            request.planningIntervalId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [{ type: QueryTags.PlanningIntervalObjective }]
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
          const data = await getPlanningIntervalsClient().deleteObjective(
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
          const data = await getPlanningIntervalsClient().getObjectiveWorkItems(
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
          const data =
            await getPlanningIntervalsClient().getObjectiveWorkItemMetrics(
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
          const data =
            await getPlanningIntervalsClient().manageObjectiveWorkItems(
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
          const data =
            await getPlanningIntervalsClient().getObjectivesHealthReport(
              request.planningIntervalKey.toString(),
              request.teamId,
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
          const data = await getPlanningIntervalsClient().getObjectiveStatuses()
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
          const types =
            await getPlanningIntervalsClient().getObjectiveStatuses()

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
          const data = await getPlanningIntervalsClient().getRisks(
            request.planningIntervalKey.toString(),
            request.includeClosed,
            request.teamId,
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
  useGetPlanningIntervalIterationsQuery,
  useGetPlanningIntervalIterationQuery,
  useGetPlanningIntervalIterationMetricsQuery,
  useGetPlanningIntervalBacklogQuery,
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
} = planningIntervalApi
