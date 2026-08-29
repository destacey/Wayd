import {
  DeactivateTeamOfTeamsRequest,
  DeactivateTeamRequest,
  FunctionalOrganizationChartDto,
  AddTeamMembershipRequest,
  UpdateTeamMembershipRequest,
  TeamMembershipDto,
  RiskListDto,
  SprintListDto,
  SprintDetailsDto,
  TeamDetailsDto,
  TeamOfTeamsDetailsDto,
  WorkStatusCategory,
  WorkItemListDto,
  SetTeamOperatingModelRequest,
  UpdateTeamOperatingModelRequest,
  TeamOperatingModelDetailsDto,
} from './../../../services/wayd-api'
import {
  CreateTeamFormValues,
  EditTeamFormValues,
  TeamListItem,
  TeamTypeName,
} from '@/src/app/organizations/types'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { getTeamsClient, getTeamsOfTeamsClient } from '@/src/services/clients'
import { BaseOptionType } from 'antd/es/select'
import { DependencyDto, WorkItemBacklogItemDto } from '@/src/services/wayd-api'
import { OptionModel } from '@/src/components/types'

export const teamApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getTeams: builder.query<TeamListItem[], boolean>({
      queryFn: async (includeInactive) => {
        try {
          const [teams, teamsOfTeams] = await Promise.all([
            getTeamsClient().getList(includeInactive),
            getTeamsOfTeamsClient().getList(includeInactive),
          ])
          const data = [
            ...(teams as TeamListItem[]),
            ...(teamsOfTeams as TeamListItem[]),
          ]
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, includeInactive) => {
        const tags = [
          { type: QueryTags.Team, id: `LIST-${includeInactive}` },
          QueryTags.Team, // Keep general tag for invalidation
        ]
        if (result) {
          result.forEach((team) => {
            tags.push({ type: QueryTags.Team, id: team.id })
          })
        }
        return tags
      },
    }),

    deactivateTeam: builder.mutation<void, DeactivateTeamRequest>({
      queryFn: async (request) => {
        try {
          const data = await getTeamsClient().deactivate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        // Invalidate the team list queries (both includeInactive true and false)
        { type: QueryTags.Team, id: 'LIST-true' },
        { type: QueryTags.Team, id: 'LIST-false' },
        // Invalidate team options queries (both includeInactive true and false)
        { type: QueryTags.TeamOptions, id: 'OPTIONS-true' },
        { type: QueryTags.TeamOptions, id: 'OPTIONS-false' },
        // Invalidate team of teams options queries
        { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-true' },
        { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-false' },
      ],
    }),

    deactivateTeamOfTeams: builder.mutation<void, DeactivateTeamOfTeamsRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await getTeamsOfTeamsClient().deactivate(
              request.id,
              request,
            )
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: (result, error, request) => [
          // Invalidate the team list queries (both includeInactive true and false)
          { type: QueryTags.Team, id: 'LIST-true' },
          { type: QueryTags.Team, id: 'LIST-false' },
          // Invalidate team options queries (both includeInactive true and false)
          { type: QueryTags.TeamOptions, id: 'OPTIONS-true' },
          { type: QueryTags.TeamOptions, id: 'OPTIONS-false' },
          // Invalidate team of teams options queries
          { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-true' },
          { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-false' },
        ],
      },
    ),

    getTeamOptions: builder.query<BaseOptionType[], boolean>({
      queryFn: async (includeInactive) => {
        try {
          const [teams, teamsOfTeams] = await Promise.all([
            getTeamsClient().getList(includeInactive),
            getTeamsOfTeamsClient().getList(includeInactive),
          ])
          const teamsData = [
            ...(teams as TeamListItem[]),
            ...(teamsOfTeams as TeamListItem[]),
          ]
          const data: BaseOptionType[] = teamsData
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((team) => ({
              label: team.name,
              value: team.id,
            }))
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, includeInactive) => {
        const tags = [
          { type: QueryTags.TeamOptions, id: `OPTIONS-${includeInactive}` },
          QueryTags.TeamOptions, // Keep general tag for invalidation
        ]
        if (result) {
          result.forEach((option) => {
            tags.push({
              type: QueryTags.TeamOptions,
              id: option.value as string,
            })
          })
        }
        return tags
      },
    }),

    getTeamBacklog: builder.query<WorkItemBacklogItemDto[], string>({
      queryFn: async (idOrCode: string) => {
        try {
          const data = await getTeamsClient().getTeamBacklog(idOrCode)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.TeamBacklog,
        { type: QueryTags.TeamBacklog, id: arg },
      ],
    }),

    getTeamWorkItems: builder.query<
      WorkItemListDto[],
      {
        idOrCode: string
        statusCategories?: WorkStatusCategory[] | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneFrom?: string | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneTo?: string | null
      }
    >({
      queryFn: async ({ idOrCode, statusCategories, doneFrom, doneTo }) => {
        try {
          const data = await getTeamsClient().getTeamWorkItems(
            idOrCode,
            statusCategories,
            doneFrom ? new Date(doneFrom) : null,
            doneTo ? new Date(doneTo) : null,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.TeamWorkItems,
        {
          type: QueryTags.TeamWorkItems,
          id: `${arg.idOrCode}-${arg.statusCategories?.join(',') ?? ''}-${arg.doneFrom ?? ''}-${arg.doneTo ?? ''}`,
        },
      ],
    }),

    getTeamDependencies: builder.query<DependencyDto[], string>({
      queryFn: async (id: string) => {
        try {
          const data = await getTeamsClient().getTeamDependencies(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.TeamDependency,
        { type: QueryTags.TeamDependency, id: arg },
      ],
    }),

    getTeamSprints: builder.query<SprintListDto[], string>({
      queryFn: async (teamId: string) => {
        try {
          const data = await getTeamsClient().getSprints(teamId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.TeamSprint,
        { type: QueryTags.TeamSprint, id: arg },
      ],
    }),

    getActiveSprint: builder.query<SprintDetailsDto, string>({
      queryFn: async (id: string) => {
        try {
          const data = await getTeamsClient().getActiveSprint(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ActiveSprint, id: arg },
      ],
    }),

    getTeamSprintOptions: builder.query<BaseOptionType[], string>({
      queryFn: async (teamId: string) => {
        try {
          const sprints = await getTeamsClient().getSprints(teamId)

          const data: BaseOptionType[] = sprints
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((team) => ({
              label: team.name,
              value: team.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.TeamSprintOption,
        { type: QueryTags.TeamSprintOption, id: arg },
      ],
    }),

    getFunctionalOrganizationChart: builder.query<
      FunctionalOrganizationChartDto,
      string | null | undefined
    >({
      queryFn: async (asOfDate: string) => {
        try {
          const date = asOfDate ? new Date(asOfDate) : undefined
          const data =
            await getTeamsClient().getFunctionalOrganizationChart(date)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        {
          type: QueryTags.FunctionalOrganizationChart,
          id: arg ?? undefined,
        },
      ],
    }),

    // TEAM OF TEAMS OPTIONS
    getTeamOfTeamsOptions: builder.query<OptionModel[], boolean>({
      queryFn: async (includeInactive = false) => {
        try {
          const teams = await getTeamsOfTeamsClient().getList(includeInactive)
          const data: OptionModel[] = teams
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((t) => ({
              value: t.id,
              label: t.isActive ? t.name : `${t.name} (Inactive)`,
            }))
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, includeInactive) => {
        const tags = [
          {
            type: QueryTags.TeamOptions,
            id: `TEAM_OF_TEAMS-${includeInactive}`,
          },
          QueryTags.TeamOptions, // Keep general tag for invalidation
        ]
        if (result) {
          result.forEach((option) => {
            tags.push({ type: QueryTags.TeamOptions, id: option.value })
          })
        }
        return tags
      },
    }),

    // TEAM MEMBERSHIPS
    getTeamMemberships: builder.query<
      TeamMembershipDto[],
      { teamId: string; enabled?: boolean }
    >({
      queryFn: async ({ teamId }) => {
        try {
          const data = await getTeamsClient().getTeamMemberships(teamId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamMembership, id: teamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
      ],
    }),

    getTeamOfTeamsMemberships: builder.query<
      TeamMembershipDto[],
      { teamId: string; enabled?: boolean }
    >({
      queryFn: async ({ teamId }) => {
        try {
          const data = await getTeamsOfTeamsClient().getTeamMemberships(teamId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamMembership, id: teamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
      ],
    }),

    // TEAM MEMBERSHIP MUTATIONS
    createTeamMembership: builder.mutation<
      void,
      { membership: AddTeamMembershipRequest; teamType: TeamTypeName }
    >({
      queryFn: async ({ membership, teamType }) => {
        try {
          let data
          if (teamType === 'Team') {
            data = await getTeamsClient().addTeamMembership(
              membership.teamId,
              membership,
            )
          } else if (teamType === 'Team of Teams') {
            data = await getTeamsOfTeamsClient().addTeamMembership(
              membership.teamId,
              membership,
            )
          } else {
            throw new Error(`Invalid team type: ${teamType}`)
          }
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { membership }) => [
        { type: QueryTags.TeamMembership, id: membership.teamId },
        { type: QueryTags.TeamMembership, id: membership.parentTeamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
      ],
    }),

    updateTeamMembership: builder.mutation<
      void,
      {
        membership: UpdateTeamMembershipRequest
        parentTeamId: string
        teamType: TeamTypeName
      }
    >({
      queryFn: async ({ membership, teamType }) => {
        try {
          let data
          if (teamType === 'Team') {
            data = await getTeamsClient().updateTeamMembership(
              membership.teamId,
              membership.teamMembershipId,
              membership,
            )
          } else if (teamType === 'Team of Teams') {
            data = await getTeamsOfTeamsClient().updateTeamMembership(
              membership.teamId,
              membership.teamMembershipId,
              membership,
            )
          } else {
            throw new Error(`Invalid team type: ${teamType}`)
          }
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { membership, parentTeamId }) => [
        { type: QueryTags.TeamMembership, id: membership.teamId },
        { type: QueryTags.TeamMembership, id: parentTeamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
      ],
    }),

    deleteTeamMembership: builder.mutation<
      void,
      {
        teamMembershipId: string
        teamId: string
        parentTeamId: string
        teamType: TeamTypeName
      }
    >({
      queryFn: async ({ teamMembershipId, teamId, teamType }) => {
        try {
          let data
          if (teamType === 'Team') {
            data = await getTeamsClient().removeTeamMembership(
              teamId,
              teamMembershipId,
            )
          } else if (teamType === 'Team of Teams') {
            data = await getTeamsOfTeamsClient().removeTeamMembership(
              teamId,
              teamMembershipId,
            )
          } else {
            throw new Error(`Invalid team type: ${teamType}`)
          }
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId, parentTeamId }) => [
        { type: QueryTags.TeamMembership, id: teamId },
        { type: QueryTags.TeamMembership, id: parentTeamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
      ],
    }),

    // TEAM RISKS
    getTeamRisks: builder.query<
      RiskListDto[],
      { id: string; includeClosed?: boolean; enabled?: boolean }
    >({
      queryFn: async ({ id, includeClosed = false }) => {
        try {
          const data = await getTeamsClient().getRisks(id, includeClosed)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { id, includeClosed }) => [
        { type: QueryTags.TeamRisk, id: `${id}-${includeClosed}` },
        { type: QueryTags.TeamRisk, id: 'LIST' },
      ],
    }),

    getTeamOfTeamsRisks: builder.query<
      RiskListDto[],
      { id: string; includeClosed?: boolean; enabled?: boolean }
    >({
      queryFn: async ({ id, includeClosed = false }) => {
        try {
          const data = await getTeamsOfTeamsClient().getRisks(id, includeClosed)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { id, includeClosed }) => [
        { type: QueryTags.TeamRisk, id: `${id}-${includeClosed}` },
        { type: QueryTags.TeamRisk, id: 'LIST' },
      ],
    }),

    // TEAM OPERATING MODELS
    getTeamOperatingModel: builder.query<
      TeamOperatingModelDetailsDto | null,
      { teamId: string; operatingModelId: string }
    >({
      queryFn: async ({ teamId, operatingModelId }) => {
        try {
          const data = await getTeamsClient().getOperatingModel(
            teamId,
            operatingModelId,
          )
          return { data }
        } catch (error: any) {
          // Return null for 404 (no operating model defined)
          if (error?.status === 404) {
            return { data: null }
          }
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { teamId, operatingModelId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: operatingModelId },
      ],
    }),

    getTeamOperatingModelAsOf: builder.query<
      TeamOperatingModelDetailsDto | null,
      { teamId: string; asOfDate?: Date | string }
    >({
      queryFn: async ({ teamId, asOfDate }) => {
        try {
          // Convert string to Date if needed
          const date = asOfDate
            ? asOfDate instanceof Date
              ? asOfDate
              : new Date(asOfDate)
            : undefined

          const data = await getTeamsClient().getOperatingModelAsOf(
            teamId,
            date,
          )
          return { data }
        } catch (error: any) {
          // Return null for 404 (no operating model defined)
          if (error?.status === 404) {
            return { data: null }
          }
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
      ],
    }),

    getTeamOperatingModels: builder.query<
      TeamOperatingModelDetailsDto[],
      string
    >({
      queryFn: async (teamId) => {
        try {
          const data = await getTeamsClient().getOperatingModels(teamId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, teamId) => [
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
      ],
    }),

    getTeamHasEverBeenScrum: builder.query<boolean, string>({
      queryFn: async (teamId) => {
        try {
          const data = await getTeamsClient().hasEverBeenScrum(teamId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, teamId) => [
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-scrum` },
      ],
    }),

    setTeamOperatingModel: builder.mutation<
      string,
      { teamId: string; request: SetTeamOperatingModelRequest }
    >({
      queryFn: async ({ teamId, request }) => {
        try {
          const data = await getTeamsClient().setOperatingModel(teamId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-scrum` },
      ],
    }),

    updateTeamOperatingModel: builder.mutation<
      void,
      {
        teamId: string
        operatingModelId: string
        request: UpdateTeamOperatingModelRequest
      }
    >({
      queryFn: async ({ teamId, operatingModelId, request }) => {
        try {
          const data = await getTeamsClient().updateOperatingModel(
            teamId,
            operatingModelId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-scrum` },
      ],
    }),

    deleteTeamOperatingModel: builder.mutation<
      void,
      { teamId: string; operatingModelId: string }
    >({
      queryFn: async ({ teamId, operatingModelId }) => {
        try {
          const data = await getTeamsClient().deleteOperatingModel(
            teamId,
            operatingModelId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-scrum` },
      ],
    }),

    getTeamDetails: builder.query<TeamDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await getTeamsClient().getById(key)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, key) => [{ type: QueryTags.Team, id: String(key) }],
    }),

    getTeamOfTeamsDetails: builder.query<TeamOfTeamsDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await getTeamsOfTeamsClient().getById(key)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, key) => [{ type: QueryTags.Team, id: String(key) }],
    }),

    createTeam: builder.mutation<TeamDetailsDto | TeamOfTeamsDetailsDto, CreateTeamFormValues>({
      queryFn: async (newTeam) => {
        try {
          const teamClient = newTeam.type === 'Team' ? getTeamsClient() : getTeamsOfTeamsClient()
          const request = {
            ...newTeam,
            activeDate: (newTeam.activeDate as any)?.format('YYYY-MM-DD'),
          }
          const created = await teamClient.create(request)
          const data = await teamClient.getById(created.key)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [
        { type: QueryTags.Team, id: 'LIST-true' },
        { type: QueryTags.Team, id: 'LIST-false' },
        { type: QueryTags.TeamOptions, id: 'OPTIONS-true' },
        { type: QueryTags.TeamOptions, id: 'OPTIONS-false' },
        { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-true' },
        { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-false' },
      ],
    }),

    updateTeam: builder.mutation<TeamDetailsDto | TeamOfTeamsDetailsDto, EditTeamFormValues>({
      queryFn: async (team) => {
        try {
          const teamClient = team.type === 'Team' ? getTeamsClient() : getTeamsOfTeamsClient()
          await teamClient.update(team.id, team)
          const data = await teamClient.getById(team.key)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, team) => [
        { type: QueryTags.Team, id: String(team.key) },
        { type: QueryTags.Team, id: 'LIST-true' },
        { type: QueryTags.Team, id: 'LIST-false' },
        { type: QueryTags.TeamOptions, id: 'OPTIONS-true' },
        { type: QueryTags.TeamOptions, id: 'OPTIONS-false' },
        { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-true' },
        { type: QueryTags.TeamOptions, id: 'TEAM_OF_TEAMS-false' },
      ],
    }),

    getTeamOperatingModelsForTeams: builder.query<
      TeamOperatingModelDetailsDto[],
      { teamIds: string[]; asOfDate?: Date | string }
    >({
      queryFn: async ({ teamIds, asOfDate }) => {
        try {
          // Convert string to Date if needed
          const date = asOfDate
            ? asOfDate instanceof Date
              ? asOfDate
              : new Date(asOfDate)
            : undefined

          const data = await getTeamsClient().getOperatingModelsForTeams(
            teamIds,
            date,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { teamIds }) => {
        const tags = [
          {
            type: QueryTags.TeamOperatingModel,
            id: `BULK-${teamIds.join(',')}`,
          },
        ]
        // Add individual team tags for cache invalidation
        teamIds.forEach((id) => {
          tags.push({ type: QueryTags.TeamOperatingModel, id })
        })
        return tags
      },
    }),
  }),
})

export const {
  useGetTeamsQuery,
  useGetTeamDetailsQuery,
  useDeactivateTeamMutation,
  useDeactivateTeamOfTeamsMutation,
  useGetTeamOptionsQuery,
  useGetTeamBacklogQuery,
  useGetTeamWorkItemsQuery,
  useGetTeamDependenciesQuery,
  useGetTeamSprintsQuery,
  useGetActiveSprintQuery,
  useGetTeamSprintOptionsQuery,
  useGetFunctionalOrganizationChartQuery,
  useGetTeamOfTeamsOptionsQuery,
  useGetTeamMembershipsQuery,
  useGetTeamOfTeamsMembershipsQuery,
  useCreateTeamMembershipMutation,
  useUpdateTeamMembershipMutation,
  useDeleteTeamMembershipMutation,
  useGetTeamRisksQuery,
  useGetTeamOfTeamsRisksQuery,
  // Operating Models
  useGetTeamOperatingModelQuery,
  useGetTeamOperatingModelAsOfQuery,
  useGetTeamOperatingModelsQuery,
  useGetTeamHasEverBeenScrumQuery,
  useSetTeamOperatingModelMutation,
  useUpdateTeamOperatingModelMutation,
  useDeleteTeamOperatingModelMutation,
  useGetTeamOperatingModelsForTeamsQuery,
  useGetTeamOfTeamsDetailsQuery,
  useCreateTeamMutation,
  useUpdateTeamMutation,
} = teamApi
