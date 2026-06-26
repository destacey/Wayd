import { getPlanningIntervalsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CreatePlanningIntervalObjectiveHealthCheckRequest,
  PlanningIntervalObjectiveHealthCheckDetailsDto,
  UpdatePlanningIntervalObjectiveHealthCheckRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface PiObjectiveHealthCheckScope {
  planningIntervalId: string
  objectiveId: string
}

export interface PiObjectiveHealthCheckRef extends PiObjectiveHealthCheckScope {
  healthCheckId: string
}

export interface CreatePiObjectiveHealthCheckArgs
  extends PiObjectiveHealthCheckScope {
  request: CreatePlanningIntervalObjectiveHealthCheckRequest
}

export interface UpdatePiObjectiveHealthCheckArgs
  extends PiObjectiveHealthCheckScope {
  request: UpdatePlanningIntervalObjectiveHealthCheckRequest
}

export const piObjectiveHealthChecksApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getObjectiveHealthChecks: builder.query<
      PlanningIntervalObjectiveHealthCheckDetailsDto[],
      PiObjectiveHealthCheckScope
    >({
      queryFn: async ({ planningIntervalId, objectiveId }) => {
        try {
          const data = await getPlanningIntervalsClient().getObjectiveHealthChecks(
            planningIntervalId,
            objectiveId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result = [], _error, { objectiveId }) => [
        { type: QueryTags.HealthChecksHealthReport, id: objectiveId },
        ...result.map(({ id }) => ({
          type: QueryTags.HealthCheck,
          id,
        })),
      ],
    }),

    getObjectiveHealthCheck: builder.query<
      PlanningIntervalObjectiveHealthCheckDetailsDto,
      PiObjectiveHealthCheckRef
    >({
      queryFn: async ({ planningIntervalId, objectiveId, healthCheckId }) => {
        try {
          const data = await getPlanningIntervalsClient().getObjectiveHealthCheck(
            planningIntervalId,
            objectiveId,
            healthCheckId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { healthCheckId }) => [
        { type: QueryTags.HealthCheck, id: healthCheckId },
      ],
    }),

    createObjectiveHealthCheck: builder.mutation<
      string,
      CreatePiObjectiveHealthCheckArgs
    >({
      queryFn: async ({ planningIntervalId, objectiveId, request }) => {
        try {
          const data = await getPlanningIntervalsClient().createObjectiveHealthCheck(
            planningIntervalId,
            objectiveId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { objectiveId }) => [
        { type: QueryTags.HealthChecksHealthReport, id: objectiveId },
        { type: QueryTags.PlanningIntervalObjective, id: 'LIST' },
        { type: QueryTags.PlanningIntervalObjective, id: objectiveId },
      ],
    }),

    updateObjectiveHealthCheck: builder.mutation<
      PlanningIntervalObjectiveHealthCheckDetailsDto,
      UpdatePiObjectiveHealthCheckArgs
    >({
      queryFn: async ({ planningIntervalId, objectiveId, request }) => {
        try {
          const data = await getPlanningIntervalsClient().updateObjectiveHealthCheck(
            planningIntervalId,
            objectiveId,
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { objectiveId, request }) => [
        { type: QueryTags.HealthCheck, id: request.id },
        { type: QueryTags.HealthChecksHealthReport, id: objectiveId },
        { type: QueryTags.PlanningIntervalObjective, id: 'LIST' },
        { type: QueryTags.PlanningIntervalObjective, id: objectiveId },
      ],
    }),

    deleteObjectiveHealthCheck: builder.mutation<
      void,
      PiObjectiveHealthCheckRef
    >({
      queryFn: async ({ planningIntervalId, objectiveId, healthCheckId }) => {
        try {
          await getPlanningIntervalsClient().deleteObjectiveHealthCheck(
            planningIntervalId,
            objectiveId,
            healthCheckId,
          )
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error: error ?? new Error('Unknown error deleting health check') }
        }
      },
      invalidatesTags: (_result, _error, { objectiveId, healthCheckId }) => [
        { type: QueryTags.HealthCheck, id: healthCheckId },
        { type: QueryTags.HealthChecksHealthReport, id: objectiveId },
        { type: QueryTags.PlanningIntervalObjective, id: 'LIST' },
        { type: QueryTags.PlanningIntervalObjective, id: objectiveId },
      ],
    }),
  }),
})

export const {
  useGetObjectiveHealthChecksQuery,
  useGetObjectiveHealthCheckQuery,
  useCreateObjectiveHealthCheckMutation,
  useUpdateObjectiveHealthCheckMutation,
  useDeleteObjectiveHealthCheckMutation,
} = piObjectiveHealthChecksApi
