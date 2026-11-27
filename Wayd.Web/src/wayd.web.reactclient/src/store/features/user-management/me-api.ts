import { toInstant } from '@/src/utils/instant'
import {
  meGetHomeContext,
  meGetIncidents,
  meGetTeamsSummary,
  meGetWorkItems,
} from '@/src/services/api/me'
import {
  HomeContextResponse,
  MyTeamsSummaryResponse,
  StatusCategory,
  TeamProductIncidentDto,
  WorkItemListDto,
} from '@/src/services/api/wayd.schemas'
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
          const data = await meGetHomeContext()
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
          const data = await meGetTeamsSummary({
            teamIds: args?.teamIds?.length ? args.teamIds : undefined,
            teamOfTeamsIds: args?.teamOfTeamsIds?.length
              ? args.teamOfTeamsIds
              : undefined,
          })
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
          const data = await meGetWorkItems({
            statusCategories: statusCategories,
            doneFrom: doneFrom ? toInstant(new Date(doneFrom)) : undefined,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.EmployeeWorkItems],
    }),
    getMyIncidents: builder.query<TeamProductIncidentDto[], void>({
      queryFn: async () => {
        try {
          const data = await meGetIncidents()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // An incident change, or a team linked to or unlinked from a product, can move one in or out of the list.
      providesTags: [
        { type: QueryTags.Incident, id: 'LIST' },
        { type: QueryTags.ProductTeamLink, id: 'LIST' },
      ],
    }),
  }),
})

export const {
  useGetHomeContextQuery,
  useGetMyIncidentsQuery,
  useGetMyTeamsSummaryQuery,
  useGetMyWorkItemsQuery,
} = meApi
