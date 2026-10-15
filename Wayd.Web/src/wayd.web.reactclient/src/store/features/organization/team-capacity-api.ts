import { getTeamsClient, getTeamsOfTeamsClient } from '@/src/services/clients'
import {
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
} from '@/src/services/wayd-api'
import { CalendarDate } from '@/src/utils'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

export type {
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
        /** Days the whole team is off, such as a sprint's team days off. */
        daysOff?: CalendarDate[]
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
                  daysOff,
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
  useGetTeamAllocationProposalQuery,
} = teamCapacityApi
