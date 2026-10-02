import { getSprintsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  NavigationDto,
  SprintBacklogItemDto,
  SprintDetailsDto,
  SprintListDto,
  SprintWorkItemMetricsDto,
  PagedResponseOfActivityLogDto,
} from '@/src/services/wayd-api'

export const sprintsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getSprints: builder.query<SprintListDto[], void>({
      queryFn: async () => {
        try {
          const data = await getSprintsClient().getSprints()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Sprint, id: 'LIST' }],
    }),

    getSprint: builder.query<SprintDetailsDto, number>({
      queryFn: async (key: number) => {
        try {
          const data = await getSprintsClient().getSprint(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [{ type: QueryTags.Sprint, id: result?.key }],
    }),

    getSprintBacklog: builder.query<SprintBacklogItemDto[], number>({
      queryFn: async (sprintKey: number) => {
        try {
          const data = await getSprintsClient().getSprintBacklog(
            sprintKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, sprintKey) => [
        { type: QueryTags.SprintBacklog, id: sprintKey },
      ],
    }),

    getSprintMetrics: builder.query<SprintWorkItemMetricsDto, number>({
      queryFn: async (sprintKey: number) => {
        try {
          const data = await getSprintsClient().getSprintMetrics(
            sprintKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, sprintKey) => [
        { type: QueryTags.SprintMetrics, id: sprintKey },
      ],
    }),

    getSprintPlanningIntervals: builder.query<NavigationDto[], number>({
      queryFn: async (sprintKey: number) => {
        try {
          const data = await getSprintsClient().getPlanningIntervals(sprintKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, sprintKey) => [
        { type: QueryTags.SprintPlanningIntervals, id: sprintKey },
      ],
    }),

    getSprintActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await getSprintsClient().getActivities(
            String(idOrKey),
            page,
            pageSize,
          )
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
  }),
})

export const {
  useGetSprintsQuery,
  useGetSprintQuery,
  useGetSprintBacklogQuery,
  useGetSprintMetricsQuery,
  useGetSprintPlanningIntervalsQuery,
  useGetSprintActivitiesQuery,
  useLazyGetSprintActivitiesQuery,
} = sprintsApi
