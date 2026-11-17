import { planningIntervalsGetObjectiveForecast } from '@/src/services/api/planning-intervals'
import { projectsGetProjectForecast } from '@/src/services/api/projects'
import { teamsGetTeamThroughputForecast } from '@/src/services/api/teams'
import { workspacesGetWorkItemForecast } from '@/src/services/api/workspaces'
import {
  TeamThroughputForecastDto,
  WorkItemForecastDto,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/** Per-request forecast choices; omitted ones take the API's defaults. */
export interface ForecastOptionsRequest {
  /** A calendar date, yyyy-MM-dd, to report the chance of finishing by. */
  targetDate?: string
  lookbackDays?: number
  ignoreDependencies?: boolean
  startedWorkFirst?: boolean
}

export interface GetWorkItemForecastRequest extends ForecastOptionsRequest {
  workspaceIdOrKey: string
  workItemKey: string
}

export interface GetObjectiveForecastRequest extends ForecastOptionsRequest {
  planningIntervalIdOrKey: string
  objectiveIdOrKey: string
}

export interface GetProjectForecastRequest extends ForecastOptionsRequest {
  projectIdOrKey: string
}

export interface GetTeamThroughputForecastRequest {
  teamIdOrCode: string
  /** A calendar date, yyyy-MM-dd. */
  targetDate: string
  lookbackDays?: number
  startedWorkFirst?: boolean
}

// A forecast changes only when the day or the data does, so a cached one
// stays good well past RTK Query's one-minute default.
const FORECAST_CACHE_SECONDS = 15 * 60

const optionsTag = (options: ForecastOptionsRequest) =>
  `${options.targetDate ?? ''}:${options.lookbackDays ?? ''}:${options.ignoreDependencies ?? ''}:${options.startedWorkFirst ?? ''}`

export const forecastsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getWorkItemForecast: builder.query<
      WorkItemForecastDto,
      GetWorkItemForecastRequest
    >({
      keepUnusedDataFor: FORECAST_CACHE_SECONDS,
      queryFn: async (request) => {
        try {
          const data = await workspacesGetWorkItemForecast(
            request.workspaceIdOrKey,
            request.workItemKey,
            {
              targetDate: request.targetDate,
              lookbackDays: request.lookbackDays,
              ignoreDependencies: request.ignoreDependencies,
              startedWorkFirst: request.startedWorkFirst,
            },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.Forecast,
          id: `work-item:${arg.workItemKey}:${optionsTag(arg)}`,
        },
      ],
    }),
    getObjectiveForecast: builder.query<
      WorkItemForecastDto,
      GetObjectiveForecastRequest
    >({
      keepUnusedDataFor: FORECAST_CACHE_SECONDS,
      queryFn: async (request) => {
        try {
          const data = await planningIntervalsGetObjectiveForecast(
            request.planningIntervalIdOrKey,
            request.objectiveIdOrKey,
            {
              targetDate: request.targetDate,
              lookbackDays: request.lookbackDays,
              ignoreDependencies: request.ignoreDependencies,
              startedWorkFirst: request.startedWorkFirst,
            },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.Forecast,
          id: `objective:${arg.planningIntervalIdOrKey}:${arg.objectiveIdOrKey}:${optionsTag(arg)}`,
        },
      ],
    }),
    getProjectForecast: builder.query<
      WorkItemForecastDto,
      GetProjectForecastRequest
    >({
      keepUnusedDataFor: FORECAST_CACHE_SECONDS,
      queryFn: async (request) => {
        try {
          const data = await projectsGetProjectForecast(
            request.projectIdOrKey,
            {
              targetDate: request.targetDate,
              lookbackDays: request.lookbackDays,
              ignoreDependencies: request.ignoreDependencies,
              startedWorkFirst: request.startedWorkFirst,
            },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.Forecast,
          id: `project:${arg.projectIdOrKey}:${optionsTag(arg)}`,
        },
      ],
    }),
    getTeamThroughputForecast: builder.query<
      TeamThroughputForecastDto,
      GetTeamThroughputForecastRequest
    >({
      keepUnusedDataFor: FORECAST_CACHE_SECONDS,
      queryFn: async ({
        teamIdOrCode,
        targetDate,
        lookbackDays,
        startedWorkFirst,
      }) => {
        try {
          const data = await teamsGetTeamThroughputForecast(teamIdOrCode, {
            targetDate: targetDate,
            lookbackDays: lookbackDays,
            startedWorkFirst: startedWorkFirst,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.Forecast,
          id: `team:${arg.teamIdOrCode}:${arg.targetDate}:${arg.lookbackDays ?? ''}:${arg.startedWorkFirst ?? ''}`,
        },
      ],
    }),
  }),
})

export const {
  useGetWorkItemForecastQuery,
  useGetObjectiveForecastQuery,
  useGetProjectForecastQuery,
  useGetTeamThroughputForecastQuery,
} = forecastsApi
