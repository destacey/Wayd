import {
  getPlanningIntervalsClient,
  getProjectsClient,
  getTeamsClient,
  getWorkspacesClient,
} from '@/src/services/clients'
import {
  TeamThroughputForecastDto,
  WorkItemForecastDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/** Per-request forecast choices; omitted ones take the API's defaults. */
export interface ForecastOptionsRequest {
  /** A calendar date, yyyy-MM-dd, to report the chance of finishing by. */
  targetDate?: string
  lookbackDays?: number
  ignoreDependencies?: boolean
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
}

const optionsTag = (options: ForecastOptionsRequest) =>
  `${options.targetDate ?? ''}:${options.lookbackDays ?? ''}:${options.ignoreDependencies ?? ''}`

export const forecastsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getWorkItemForecast: builder.query<
      WorkItemForecastDto,
      GetWorkItemForecastRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getWorkspacesClient().getWorkItemForecast(
            request.workspaceIdOrKey,
            request.workItemKey,
            request.targetDate,
            request.lookbackDays,
            request.ignoreDependencies,
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
      queryFn: async (request) => {
        try {
          const data = await getPlanningIntervalsClient().getObjectiveForecast(
            request.planningIntervalIdOrKey,
            request.objectiveIdOrKey,
            request.targetDate,
            request.lookbackDays,
            request.ignoreDependencies,
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
      queryFn: async (request) => {
        try {
          const data = await getProjectsClient().getProjectForecast(
            request.projectIdOrKey,
            request.targetDate,
            request.lookbackDays,
            request.ignoreDependencies,
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
      queryFn: async ({ teamIdOrCode, targetDate, lookbackDays }) => {
        try {
          const data = await getTeamsClient().getTeamThroughputForecast(
            teamIdOrCode,
            targetDate,
            lookbackDays,
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
          id: `team:${arg.teamIdOrCode}:${arg.targetDate}:${arg.lookbackDays ?? ''}`,
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
