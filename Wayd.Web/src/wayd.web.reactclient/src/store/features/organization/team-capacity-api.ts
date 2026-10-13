import { getTeamsClient, getTeamsOfTeamsClient } from '@/src/services/clients'
import {
  TeamAllocationProposalDto,
  TeamAllocationProposalRowDto,
  TeamCapacityAllocationDto,
  TeamCapacityDto,
  TeamCapacityPersonDto,
  TeamCapacityRoleDto,
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
  TeamCapacityRoleDto,
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

export const { useGetTeamCapacityQuery, useGetTeamAllocationProposalQuery } =
  teamCapacityApi
