import { getTeamsClient, getTeamsOfTeamsClient } from '@/src/services/clients'
import {
  CapacityMeasuresDto,
  CapacityReportInterval,
  TeamAllocationProposalDto,
  TeamAllocationProposalRowDto,
  TeamCapacityAllocationDto,
  TeamCapacityDto,
  TeamCapacityPersonDto,
  TeamCapacityPlanDto,
  TeamCapacityReportDto,
  TeamCapacityReportLineDto,
  TeamCapacityReportPersonDto,
  TeamCapacityReportRoleDto,
  TeamCapacityReportTeamDto,
  TeamCapacityRoleDto,
  TeamCapacityRollupDto,
  TeamCapacityRollupNodeDto,
  TeamCapacityTimeAwayDto,
  TeamDayOffDto,
} from '@/src/services/wayd-api'
import { CalendarDate, teamDayOffParam } from '@/src/utils'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

export { CapacityReportInterval }

export type {
  CapacityMeasuresDto,
  TeamCapacityReportDto,
  TeamCapacityReportLineDto,
  TeamCapacityReportPersonDto,
  TeamCapacityReportRoleDto,
  TeamCapacityReportTeamDto,
  TeamAllocationProposalDto,
  TeamAllocationProposalRowDto,
  TeamCapacityAllocationDto,
  TeamCapacityDto,
  TeamCapacityPersonDto,
  TeamCapacityPlanDto,
  TeamCapacityRoleDto,
  TeamCapacityRollupDto,
  TeamCapacityRollupNodeDto,
  TeamCapacityTimeAwayDto,
}

/** Which of the two team endpoints a capacity read goes to. */
export type CapacityTeamType = 'Team' | 'TeamOfTeams'

const clientFor = (teamType: CapacityTeamType) =>
  teamType === 'Team' ? getTeamsClient() : getTeamsOfTeamsClient()

export const teamCapacityApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getTeamCapacity: builder.query<
      TeamCapacityDto,
      {
        teamId: string
        teamType: CapacityTeamType
        from: CalendarDate
        to: CalendarDate
      }
    >({
      queryFn: async ({ teamId, teamType, from, to }) => {
        try {
          const data = await clientFor(teamType).getCapacity(teamId, from, to)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.TeamCapacity, id: arg.teamId },
      ],
    }),

    getTeamCapacityPlan: builder.query<
      TeamCapacityPlanDto,
      {
        teamId: string
        teamType: CapacityTeamType
        from: CalendarDate
        to: CalendarDate
        /** Days the whole team is off for some or all of, such as a sprint's team days off. */
        daysOff?: TeamDayOffDto[]
      }
    >({
      queryFn: async ({ teamId, teamType, from, to, daysOff }) => {
        try {
          const data =
            teamType === 'Team'
              ? await getTeamsClient().getCapacityPlan(
                  teamId,
                  from,
                  to,
                  daysOff?.map(teamDayOffParam),
                )
              : await getTeamsOfTeamsClient().getCapacityPlan(teamId, from, to)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.TeamCapacity, id: arg.teamId },
      ],
    }),

    getTeamOfTeamsCapacityRollup: builder.query<
      TeamCapacityRollupDto,
      { teamId: string; from: CalendarDate; to: CalendarDate }
    >({
      queryFn: async ({ teamId, from, to }) => {
        try {
          const data = await getTeamsOfTeamsClient().getCapacityRollup(
            teamId,
            from,
            to,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The rollup is made of the allocations of every team beneath this one
      // and of which teams those are, so a change to either refetches it.
      providesTags: (result, error, arg) => [
        { type: QueryTags.TeamCapacity, id: arg.teamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
      ],
    }),

    getTeamCapacityReport: builder.query<
      TeamCapacityReportDto,
      {
        teamId: string
        teamType: CapacityTeamType
        from: CalendarDate
        to: CalendarDate
        interval?: CapacityReportInterval
      }
    >({
      queryFn: async ({ teamId, teamType, from, to, interval }) => {
        try {
          const data = await clientFor(teamType).getCapacityReport(
            teamId,
            from,
            to,
            interval,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A team of teams' report is made of the allocations of every team
      // beneath it and of which teams those are, so a change to either
      // refetches it.
      providesTags: (result, error, arg) => [
        { type: QueryTags.TeamCapacity, id: arg.teamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
      ],
    }),

    getTeamAllocationProposal: builder.query<
      TeamAllocationProposalDto,
      { teamId: string; teamType: CapacityTeamType; from: CalendarDate }
    >({
      queryFn: async ({ teamId, teamType, from }) => {
        try {
          const data = await clientFor(teamType).getAllocationProposal(
            teamId,
            from,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.TeamCapacity, id: arg.teamId },
      ],
    }),
  }),
})

export const {
  useGetTeamCapacityQuery,
  useGetTeamCapacityPlanQuery,
  useGetTeamOfTeamsCapacityRollupQuery,
  useGetTeamCapacityReportQuery,
  useGetTeamAllocationProposalQuery,
} = teamCapacityApi
