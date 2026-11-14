import { getMeClient } from '@/src/services/clients'
import {
  HomeContextResponse,
  MyTeamsSummaryResponse,
  StatusCategory,
  WorkItemListDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/** Which of the caller's work items to fetch from `api/me/work-items`. */
export interface MyWorkItemsArgs {
  statusCategories?: StatusCategory[]
  /** ISO 8601 instant; keeps only work items finished at or after it. */
  doneFrom?: string
}

/**
 * Which of the caller's teams and teams of teams `api/me/teams/summary`
 * summarises; either left out or empty means all of them.
 */
export interface MyTeamsSummaryArgs {
  teamIds?: string[]
  teamOfTeamsIds?: string[]
}

/** The caller's own records, from `api/me`. */
export const meApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getHomeContext: builder.query<HomeContextResponse, void>({
      queryFn: async () => {
        try {
          const data = await getMeClient().getHomeContext()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
    getMyTeamsSummary: builder.query<
      MyTeamsSummaryResponse,
      MyTeamsSummaryArgs | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getMeClient().getTeamsSummary(
            args?.teamIds?.length ? args.teamIds : undefined,
            args?.teamOfTeamsIds?.length ? args.teamOfTeamsIds : undefined,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
    getMyWorkItems: builder.query<WorkItemListDto[], MyWorkItemsArgs>({
      queryFn: async ({ statusCategories, doneFrom }) => {
        try {
          const data = await getMeClient().getWorkItems(
            statusCategories,
            doneFrom ? new Date(doneFrom) : null,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.EmployeeWorkItems],
    }),
  }),
})

export const {
  useGetHomeContextQuery,
  useGetMyTeamsSummaryQuery,
  useGetMyWorkItemsQuery,
} = meApi
