import { getSprintsClient } from '@/src/services/clients'
import { isApiError } from '@/src/utils'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  NavigationDto,
  SprintBacklogItemDto,
  SprintDetailsDto,
  SprintListDto,
  SprintBurnDto,
  SprintScopeDto,
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

    getSprintScope: builder.query<SprintScopeDto, number>({
      queryFn: async (sprintKey: number) => {
        try {
          const data = await getSprintsClient().getSprintScope(
            sprintKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, sprintKey) => [
        { type: QueryTags.SprintScope, id: sprintKey },
      ],
    }),

    getSprintBurn: builder.query<SprintBurnDto, number>({
      queryFn: async (sprintKey: number) => {
        try {
          const data = await getSprintsClient().getSprintBurn(
            sprintKey.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, sprintKey) => [
        { type: QueryTags.SprintBurn, id: sprintKey },
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

    startSprint: builder.mutation<
      void,
      {
        id: string
        key: number
        completeOpenSprintId?: string
        startedAt?: Date
        openSprint?: NavigationDto
      }
    >({
      queryFn: async ({ id, completeOpenSprintId, startedAt }) => {
        try {
          const data = await getSprintsClient().start(id, {
            completeOpenSprintId,
            startedAt,
          })
          return { data }
        } catch (error) {
          logUnlessRefused(error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, key, openSprint }) => [
        ...sprintLifecycleTags(id, key),
        ...(openSprint
          ? sprintLifecycleTags(openSprint.id, openSprint.key)
          : []),
      ],
    }),

    completeSprint: builder.mutation<
      void,
      { id: string; key: number; completedAt?: Date }
    >({
      queryFn: async ({ id, completedAt }) => {
        try {
          const data = await getSprintsClient().complete(id, { completedAt })
          return { data }
        } catch (error) {
          logUnlessRefused(error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, key }) =>
        sprintLifecycleTags(id, key),
    }),

    reopenSprint: builder.mutation<void, { id: string; key: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getSprintsClient().reopen(id)
          return { data }
        } catch (error) {
          logUnlessRefused(error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, key }) =>
        sprintLifecycleTags(id, key),
    }),

    correctSprintActualDates: builder.mutation<
      void,
      {
        sprints: {
          id: string
          key: number
          started?: Date
          completed?: Date
        }[]
      }
    >({
      queryFn: async ({ sprints }) => {
        try {
          const data = await getSprintsClient().correctActualDates({
            sprints: sprints.map(({ id, started, completed }) => ({
              sprintId: id,
              started,
              completed,
            })),
          })
          return { data }
        } catch (error) {
          logUnlessRefused(error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { sprints }) =>
        sprints.flatMap(({ id, key }) => sprintLifecycleTags(id, key)),
    }),
  }),
})

// A 400 or 422 is the server refusing the change, which the form that sent it
// shows. Logged as an error, Next's dev overlay reports it as a crash.
function logUnlessRefused(error: unknown) {
  if (isApiError(error) && (error.status === 400 || error.status === 422))
    return
  console.error('API Error:', error)
}

// A lifecycle change moves the sprint's state, so every view of the team's
// sprints goes stale with it. The team tags are invalidated by type: they are
// keyed by team, which these mutations are not given.
function sprintLifecycleTags(id: string, key: number) {
  return [
    { type: QueryTags.Sprint, id: key },
    { type: QueryTags.Sprint, id: 'LIST' },
    { type: QueryTags.SprintMetrics, id: key },
    // The sprint's actual dates bound its scope, so every team sprint's report
    // can move with them — a start completes the previous sprint too.
    QueryTags.SprintScope,
    QueryTags.SprintBurn,
    { type: QueryTags.ActivityLog, id },
    QueryTags.TeamSprint,
    QueryTags.ActiveSprint,
    QueryTags.TeamSprintOption,
  ] as const
}

export const {
  useGetSprintsQuery,
  useGetSprintQuery,
  useGetSprintBacklogQuery,
  useGetSprintMetricsQuery,
  useGetSprintScopeQuery,
  useGetSprintBurnQuery,
  useGetSprintPlanningIntervalsQuery,
  useGetSprintActivitiesQuery,
  useLazyGetSprintActivitiesQuery,
  useStartSprintMutation,
  useCompleteSprintMutation,
  useReopenSprintMutation,
  useCorrectSprintActualDatesMutation,
} = sprintsApi
