import { type Instant, toInstant } from '@/src/utils/instant'
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
  TeamSprintMetricsDto,
  TeamCadenceMetricsDto,
  TeamDetailsDto,
  TeamOfTeamsDetailsDto,
  StatusCategory,
  WorkItemListDto,
  SetTeamOperatingModelRequest,
  UpdateTeamOperatingModelRequest,
  TeamOperatingModelDetailsDto,
  PagedResponseOfActivityLogDto,
  OperatingModelDefaultsDto,
  SetTeamOfTeamsOperatingModelRequest,
  UpdateTeamOfTeamsOperatingModelRequest,
  TeamOfTeamsOperatingModelDetailsDto,
} from '@/src/services/api/wayd.schemas'
import {
  CreateTeamFormValues,
  EditTeamFormValues,
  TeamListItem,
  TeamTypeName,
} from '@/src/app/organizations/types'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  teamsAddTeamMembership,
  teamsCreate,
  teamsDeactivate,
  teamsDeleteOperatingModel,
  teamsGetActiveSprint,
  teamsGetActivities,
  teamsGetById,
  teamsGetCadenceMetrics,
  teamsGetDependencyAnalysis,
  teamsGetFunctionalOrganizationChart,
  teamsGetList,
  teamsGetOperatingModel,
  teamsGetOperatingModelAsOf,
  teamsGetOperatingModelDefaults,
  teamsGetOperatingModels,
  teamsGetOperatingModelsForTeams,
  teamsGetRisks,
  teamsGetSprintMetrics,
  teamsGetSprints,
  teamsGetTeamBacklog,
  teamsGetTeamBacklogHealth,
  teamsGetTeamDependencies,
  teamsGetTeamMemberships,
  teamsGetTeamWorkDistribution,
  teamsGetTeamWorkItems,
  teamsHasEverBeenScrum,
  teamsRemoveTeamMembership,
  teamsSetOperatingModel,
  teamsUpdate,
  teamsUpdateOperatingModel,
  teamsUpdateTeamMembership,
} from '@/src/services/api/teams'
import {
  teamsOfTeamsAddTeamMembership,
  teamsOfTeamsCreate,
  teamsOfTeamsDeactivate,
  teamsOfTeamsDeleteOperatingModel,
  teamsOfTeamsGetActivities,
  teamsOfTeamsGetById,
  teamsOfTeamsGetDependencies,
  teamsOfTeamsGetDependencyAnalysis,
  teamsOfTeamsGetList,
  teamsOfTeamsGetOperatingModel,
  teamsOfTeamsGetOperatingModelDefaults,
  teamsOfTeamsGetOperatingModels,
  teamsOfTeamsGetRisks,
  teamsOfTeamsGetTeamMemberships,
  teamsOfTeamsGetWorkDistribution,
  teamsOfTeamsRemoveTeamMembership,
  teamsOfTeamsSetOperatingModel,
  teamsOfTeamsUpdate,
  teamsOfTeamsUpdateOperatingModel,
  teamsOfTeamsUpdateTeamMembership,
} from '@/src/services/api/teams-of-teams'
import type { BaseOptionType } from 'antd/es/select'
import {
  BacklogHealthThresholds,
  DependencyDto,
  WorkDistributionDimension,
  WorkDistributionMeasure,
  TeamWorkDistributionDto,
  TeamBacklogHealthDto,
  TeamDependencyAnalysisDto,
  ThemeCounting,
  UnestimatedHandling,
  WorkItemBacklogItemDto,
} from '@/src/services/api/wayd.schemas'
import { OptionModel } from '@/src/components/types'
import { CalendarDate, toCalendarDate } from '@/src/utils'

export interface GetTeamBacklogHealthRequest {
  teamIdOrCode: string
  lookbackDays?: number
  /** Omitted thresholds take the API's defaults. */
  thresholds?: Partial<BacklogHealthThresholds>
}

export interface GetTeamWorkDistributionRequest {
  /** Teams of teams are served by their own endpoint, which rolls up every team beneath them. */
  teamType: 'team' | 'team-of-teams'
  teamIdOrCode: string
  /** yyyy-MM-dd, inclusive. */
  from: string
  /** yyyy-MM-dd, inclusive. */
  to: string
  dimension: WorkDistributionDimension
  measure: WorkDistributionMeasure
  unestimated: UnestimatedHandling
  themeCounting: ThemeCounting
}

export interface GetTeamDependencyAnalysisRequest {
  /** Teams of teams are served by their own endpoint, which covers every team beneath them. */
  teamType: 'team' | 'team-of-teams'
  teamIdOrCode: string
  /** yyyy-MM-dd, inclusive. */
  from: string
  /** yyyy-MM-dd, inclusive. */
  to: string
}

/**
 * The operating-model `asOfDate` parameters are bound as a `DateTime` and read
 * as its UTC date, and the client sends `toISOString()`. UTC midnight of the
 * day is the only instant that names the same day in every viewer's zone.
 */
const asOfDateParam = (asOfDate?: CalendarDate): Instant | undefined =>
  asOfDate ? toInstant(new Date(`${asOfDate}T00:00:00Z`)) : undefined

/**
 * What a team's operating model changes besides itself: the team's details
 * show its sizing method, and the server measures sprint and iteration
 * metrics, backlog health and work distribution in it. Invalidated by type, since a
 * sprint, iteration or team of teams is cached under its own key, not the
 * team's.
 */
const operatingModelDependentTags = [
  QueryTags.Team,
  QueryTags.TeamBacklog,
  QueryTags.SprintMetrics,
  QueryTags.PlanningIntervalIterationMetrics,
] as const

export const teamApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getTeams: builder.query<TeamListItem[], boolean>({
      queryFn: async (includeInactive) => {
        try {
          const [teams, teamsOfTeams] = await Promise.all([
            teamsGetList({ includeInactive: includeInactive }),
            teamsOfTeamsGetList({ includeInactive: includeInactive }),
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

    deactivateTeam: builder.mutation<null, DeactivateTeamRequest>({
      queryFn: async (request) => {
        try {
          await teamsDeactivate(request.id, request)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
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
        { type: QueryTags.ActivityLog, id: request.id },
      ],
    }),

    deactivateTeamOfTeams: builder.mutation<null, DeactivateTeamOfTeamsRequest>(
      {
        queryFn: async (request) => {
          try {
            await teamsOfTeamsDeactivate(request.id, request)
            // The API answers 204 with no body; RTK Query rejects undefined as a result.
            return { data: null }
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
          { type: QueryTags.ActivityLog, id: request.id },
        ],
      },
    ),

    getTeamOptions: builder.query<BaseOptionType[], boolean>({
      queryFn: async (includeInactive) => {
        try {
          const [teams, teamsOfTeams] = await Promise.all([
            teamsGetList({ includeInactive: includeInactive }),
            teamsOfTeamsGetList({ includeInactive: includeInactive }),
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
          const data = await teamsGetTeamBacklog(idOrCode)
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

    getTeamWorkDistribution: builder.query<
      TeamWorkDistributionDto,
      GetTeamWorkDistributionRequest
    >({
      queryFn: async (request) => {
        try {
          const params = {
            From: request.from,
            To: request.to,
            Dimension: request.dimension,
            Measure: request.measure,
            Unestimated: request.unestimated,
            ThemeCounting: request.themeCounting,
          }
          const data =
            request.teamType === 'team-of-teams'
              ? await teamsOfTeamsGetWorkDistribution(
                  request.teamIdOrCode,
                  params,
                )
              : await teamsGetTeamWorkDistribution(request.teamIdOrCode, params)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Built from synced work items, so it goes stale whenever the backlog does.
      providesTags: (result, error, arg) => [
        QueryTags.TeamBacklog,
        { type: QueryTags.TeamBacklog, id: arg.teamIdOrCode },
      ],
    }),

    getTeamBacklogHealth: builder.query<
      TeamBacklogHealthDto,
      GetTeamBacklogHealthRequest
    >({
      queryFn: async ({ teamIdOrCode, lookbackDays, thresholds = {} }) => {
        try {
          const data = await teamsGetTeamBacklogHealth(teamIdOrCode, {
            LookbackDays: lookbackDays,
            StaleDays: thresholds.staleDays,
            OldProposedDays: thresholds.oldProposedDays,
            AgingWipPercentile: thresholds.agingWipPercentile,
            OversizedPercentile: thresholds.oversizedPercentile,
            ReadinessWindowWeeks: thresholds.readinessWindowWeeks,
            ReadinessFallbackItems: thresholds.readinessFallbackItems,
            AtRiskPercent: thresholds.atRiskPercent,
            UnhealthyPercent: thresholds.unhealthyPercent,
            RunwayAtRiskWeeks: thresholds.runwayAtRiskWeeks,
            RunwayUnhealthyWeeks: thresholds.runwayUnhealthyWeeks,
            RunwayTooLongWeeks: thresholds.runwayTooLongWeeks,
            NetFlowAtRisk: thresholds.netFlowAtRisk,
            NetFlowUnhealthy: thresholds.netFlowUnhealthy,
            WipLoadAtRisk: thresholds.wipLoadAtRisk,
            WipLoadUnhealthy: thresholds.wipLoadUnhealthy,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Graded from the backlog, so it goes stale whenever the backlog does.
      providesTags: (result, error, arg) => [
        QueryTags.TeamBacklog,
        { type: QueryTags.TeamBacklog, id: arg.teamIdOrCode },
      ],
    }),

    getTeamWorkItems: builder.query<
      WorkItemListDto[],
      {
        idOrCode: string
        statusCategories?: StatusCategory[] | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneFrom?: string | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneTo?: string | null
      }
    >({
      queryFn: async ({ idOrCode, statusCategories, doneFrom, doneTo }) => {
        try {
          const data = await teamsGetTeamWorkItems(idOrCode, {
            statusCategories: statusCategories ?? undefined,
            doneFrom: doneFrom ? toInstant(new Date(doneFrom)) : undefined,
            doneTo: doneTo ? toInstant(new Date(doneTo)) : undefined,
          })
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
          const data = await teamsGetTeamDependencies(id)
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
          const data = await teamsGetSprints(teamId)
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

    getTeamSprintMetrics: builder.query<
      TeamSprintMetricsDto,
      { teamId: string; sprintWindow?: number }
    >({
      queryFn: async ({ teamId, sprintWindow }) => {
        try {
          const data = await teamsGetSprintMetrics(teamId, {
            sprintWindow: sprintWindow,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A sprint's dates, type or scope changing changes which sprints count and what they report.
      providesTags: (result, error, arg) => [
        QueryTags.TeamSprint,
        { type: QueryTags.TeamSprint, id: arg.teamId },
        QueryTags.SprintScope,
        // The organization's sprint window decides how many sprints count.
        { type: QueryTags.SystemSettings, id: 'sprint-metrics' },
        // The team's operating model sets the unit, the zone and the commitment
        // point each sprint is measured with; a sprint with no model takes the
        // system time zone and the scheduling defaults.
        { type: QueryTags.TeamOperatingModel, id: arg.teamId },
        { type: QueryTags.SystemSettings, id: 'general' },
        { type: QueryTags.SystemSettings, id: 'scheduling' },
      ],
    }),

    getTeamCadenceMetrics: builder.query<
      TeamCadenceMetricsDto,
      { teamId: string; periodWindow?: number }
    >({
      queryFn: async ({ teamId, periodWindow }) => {
        try {
          const data = await teamsGetCadenceMetrics(teamId, {
            periodWindow: periodWindow,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        // The operating models set the cadence, zone and unit each period is measured in.
        { type: QueryTags.TeamOperatingModel, id: arg.teamId },
        // The organization's sprint window decides how many periods count.
        { type: QueryTags.SystemSettings, id: 'sprint-metrics' },
      ],
    }),

    getActiveSprint: builder.query<SprintDetailsDto | null, string>({
      queryFn: async (id: string) => {
        try {
          const data = await teamsGetActiveSprint(id)
          return { data }
        } catch (error: any) {
          // 404 is the API's answer for a team with no sprint in progress.
          if (error?.status === 404) {
            return { data: null }
          }
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
          const sprints = await teamsGetSprints(teamId)

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
          const date = asOfDate ? toInstant(new Date(asOfDate)) : undefined
          const data = await teamsGetFunctionalOrganizationChart({
            asOfDate: date,
          })
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
          const teams = await teamsOfTeamsGetList({
            includeInactive: includeInactive,
          })
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
          const data = await teamsGetTeamMemberships(teamId)
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
          const data = await teamsOfTeamsGetTeamMemberships(teamId)
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
      null,
      { membership: AddTeamMembershipRequest; teamType: TeamTypeName }
    >({
      queryFn: async ({ membership, teamType }) => {
        try {
          if (teamType === 'Team') {
            await teamsAddTeamMembership(membership.teamId, membership)
          } else if (teamType === 'Team of Teams') {
            await teamsOfTeamsAddTeamMembership(membership.teamId, membership)
          } else {
            throw new Error(`Invalid team type: ${teamType}`)
          }
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { membership }) => [
        { type: QueryTags.TeamMembership, id: membership.teamId },
        { type: QueryTags.TeamMembership, id: membership.parentTeamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
        { type: QueryTags.ActivityLog, id: membership.teamId },
        { type: QueryTags.ActivityLog, id: membership.parentTeamId },
      ],
    }),

    updateTeamMembership: builder.mutation<
      null,
      {
        membership: UpdateTeamMembershipRequest
        parentTeamId: string
        teamType: TeamTypeName
      }
    >({
      queryFn: async ({ membership, teamType }) => {
        try {
          if (teamType === 'Team') {
            await teamsUpdateTeamMembership(
              membership.teamId,
              membership.teamMembershipId,
              membership,
            )
          } else if (teamType === 'Team of Teams') {
            await teamsOfTeamsUpdateTeamMembership(
              membership.teamId,
              membership.teamMembershipId,
              membership,
            )
          } else {
            throw new Error(`Invalid team type: ${teamType}`)
          }
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { membership, parentTeamId }) => [
        { type: QueryTags.TeamMembership, id: membership.teamId },
        { type: QueryTags.TeamMembership, id: parentTeamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
        { type: QueryTags.ActivityLog, id: membership.teamId },
        { type: QueryTags.ActivityLog, id: parentTeamId },
      ],
    }),

    deleteTeamMembership: builder.mutation<
      null,
      {
        teamMembershipId: string
        teamId: string
        parentTeamId: string
        teamType: TeamTypeName
      }
    >({
      queryFn: async ({ teamMembershipId, teamId, teamType }) => {
        try {
          if (teamType === 'Team') {
            await teamsRemoveTeamMembership(teamId, teamMembershipId)
          } else if (teamType === 'Team of Teams') {
            await teamsOfTeamsRemoveTeamMembership(teamId, teamMembershipId)
          } else {
            throw new Error(`Invalid team type: ${teamType}`)
          }
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId, parentTeamId }) => [
        { type: QueryTags.TeamMembership, id: teamId },
        { type: QueryTags.TeamMembership, id: parentTeamId },
        { type: QueryTags.TeamMembership, id: 'LIST' },
        { type: QueryTags.ActivityLog, id: teamId },
        { type: QueryTags.ActivityLog, id: parentTeamId },
      ],
    }),

    // TEAM RISKS
    getTeamRisks: builder.query<
      RiskListDto[],
      { id: string; includeClosed?: boolean; enabled?: boolean }
    >({
      queryFn: async ({ id, includeClosed = false }) => {
        try {
          const data = await teamsGetRisks(id, { includeClosed: includeClosed })
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
          const data = await teamsOfTeamsGetRisks(id, {
            includeClosed: includeClosed,
          })
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

    getTeamOfTeamsDependencies: builder.query<DependencyDto[], string>({
      queryFn: async (id) => {
        try {
          const data = await teamsOfTeamsGetDependencies(id)
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

    getTeamDependencyAnalysis: builder.query<
      TeamDependencyAnalysisDto,
      GetTeamDependencyAnalysisRequest
    >({
      queryFn: async ({ teamType, teamIdOrCode, from, to }) => {
        try {
          const data =
            teamType === 'team-of-teams'
              ? await teamsOfTeamsGetDependencyAnalysis(teamIdOrCode, {
                  From: from,
                  To: to,
                })
              : await teamsGetDependencyAnalysis(teamIdOrCode, {
                  From: from,
                  To: to,
                })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Built from synced dependencies and work items, so it goes stale with either. Tagged by type only:
      // the request names the team by code, and an id-scoped invalidation would name it by its id.
      providesTags: [QueryTags.TeamDependency, QueryTags.TeamBacklog],
    }),

    // TEAM OPERATING MODELS
    getOperatingModelDefaults: builder.query<
      OperatingModelDefaultsDto,
      { teamId: string; teamType: TeamTypeName; startDate: string }
    >({
      queryFn: async ({ teamId, teamType, startDate }) => {
        try {
          const getDefaults =
            teamType === 'Team'
              ? teamsGetOperatingModelDefaults
              : teamsOfTeamsGetOperatingModelDefaults
          const data = await getDefaults(teamId, { startDate })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The zone comes from the parent's operating model through the team's
      // membership, or else the system time zone in the general settings; the
      // grace period comes from the scheduling settings.
      providesTags: (result, error, { teamId }) => [
        { type: QueryTags.SystemSettings, id: 'general' },
        { type: QueryTags.SystemSettings, id: 'scheduling' },
        { type: QueryTags.TeamMembership, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: 'defaults' },
      ],
    }),

    getTeamOperatingModel: builder.query<
      TeamOperatingModelDetailsDto | null,
      { teamId: string; operatingModelId: string }
    >({
      queryFn: async ({ teamId, operatingModelId }) => {
        try {
          const data = await teamsGetOperatingModel(teamId, operatingModelId)
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
      { teamId: string; asOfDate?: CalendarDate }
    >({
      queryFn: async ({ teamId, asOfDate }) => {
        try {
          const data = await teamsGetOperatingModelAsOf(teamId, {
            asOfDate: asOfDateParam(asOfDate),
          })
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
          const data = await teamsGetOperatingModels(teamId)
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
          const data = await teamsHasEverBeenScrum(teamId)
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
          const data = await teamsSetOperatingModel(teamId, request)
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
        { type: QueryTags.ActivityLog, id: teamId },
        ...operatingModelDependentTags,
      ],
    }),

    updateTeamOperatingModel: builder.mutation<
      null,
      {
        teamId: string
        operatingModelId: string
        request: UpdateTeamOperatingModelRequest
      }
    >({
      queryFn: async ({ teamId, operatingModelId, request }) => {
        try {
          await teamsUpdateOperatingModel(teamId, operatingModelId, request)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-scrum` },
        { type: QueryTags.ActivityLog, id: teamId },
        ...operatingModelDependentTags,
      ],
    }),

    deleteTeamOperatingModel: builder.mutation<
      null,
      { teamId: string; operatingModelId: string }
    >({
      queryFn: async ({ teamId, operatingModelId }) => {
        try {
          await teamsDeleteOperatingModel(teamId, operatingModelId)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-scrum` },
        { type: QueryTags.ActivityLog, id: teamId },
        ...operatingModelDependentTags,
      ],
    }),

    // TEAM OF TEAMS OPERATING MODELS
    getTeamOfTeamsOperatingModels: builder.query<
      TeamOfTeamsOperatingModelDetailsDto[],
      string
    >({
      queryFn: async (teamId) => {
        try {
          const data = await teamsOfTeamsGetOperatingModels(teamId)
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

    getTeamOfTeamsOperatingModel: builder.query<
      TeamOfTeamsOperatingModelDetailsDto,
      { teamId: string; operatingModelId: string }
    >({
      queryFn: async ({ teamId, operatingModelId }) => {
        try {
          const data = await teamsOfTeamsGetOperatingModel(
            teamId,
            operatingModelId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { teamId, operatingModelId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: operatingModelId },
      ],
    }),

    setTeamOfTeamsOperatingModel: builder.mutation<
      string,
      { teamId: string; request: SetTeamOfTeamsOperatingModelRequest }
    >({
      queryFn: async ({ teamId, request }) => {
        try {
          const data = await teamsOfTeamsSetOperatingModel(teamId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: 'defaults' },
        { type: QueryTags.ActivityLog, id: teamId },
      ],
    }),

    updateTeamOfTeamsOperatingModel: builder.mutation<
      null,
      {
        teamId: string
        operatingModelId: string
        request: UpdateTeamOfTeamsOperatingModelRequest
      }
    >({
      queryFn: async ({ teamId, operatingModelId, request }) => {
        try {
          await teamsOfTeamsUpdateOperatingModel(
            teamId,
            operatingModelId,
            request,
          )
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: 'defaults' },
        { type: QueryTags.ActivityLog, id: teamId },
      ],
    }),

    deleteTeamOfTeamsOperatingModel: builder.mutation<
      null,
      { teamId: string; operatingModelId: string }
    >({
      queryFn: async ({ teamId, operatingModelId }) => {
        try {
          await teamsOfTeamsDeleteOperatingModel(teamId, operatingModelId)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { teamId }) => [
        { type: QueryTags.TeamOperatingModel, id: teamId },
        { type: QueryTags.TeamOperatingModel, id: `${teamId}-history` },
        { type: QueryTags.TeamOperatingModel, id: 'defaults' },
        { type: QueryTags.ActivityLog, id: teamId },
      ],
    }),

    getTeamDetails: builder.query<TeamDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await teamsGetById(key)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, key) => [
        { type: QueryTags.Team, id: String(key) },
      ],
    }),

    getTeamOfTeamsDetails: builder.query<TeamOfTeamsDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await teamsOfTeamsGetById(key)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, key) => [
        { type: QueryTags.Team, id: String(key) },
      ],
    }),

    createTeam: builder.mutation<
      TeamDetailsDto | TeamOfTeamsDetailsDto,
      CreateTeamFormValues
    >({
      queryFn: async (newTeam) => {
        try {
          const request = {
            ...newTeam,
            activeDate: toCalendarDate(newTeam.activeDate),
          }
          const data =
            newTeam.type === 'Team'
              ? await teamsGetById((await teamsCreate(request)).key)
              : await teamsOfTeamsGetById(
                  (await teamsOfTeamsCreate(request)).key,
                )
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

    updateTeam: builder.mutation<
      TeamDetailsDto | TeamOfTeamsDetailsDto,
      EditTeamFormValues
    >({
      queryFn: async (team) => {
        try {
          const data =
            team.type === 'Team'
              ? await teamsUpdate(team.id, team).then(() =>
                  teamsGetById(team.key),
                )
              : await teamsOfTeamsUpdate(team.id, team).then(() =>
                  teamsOfTeamsGetById(team.key),
                )
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
        { type: QueryTags.ActivityLog, id: team.id },
        { type: QueryTags.ActivityLog, id: String(team.key) },
      ],
    }),

    getTeamOperatingModelsForTeams: builder.query<
      TeamOperatingModelDetailsDto[],
      { teamIds: string[]; asOfDate?: CalendarDate }
    >({
      queryFn: async ({ teamIds, asOfDate }) => {
        try {
          const data = await teamsGetOperatingModelsForTeams({
            teamIds: teamIds,
            asOfDate: asOfDateParam(asOfDate),
          })
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

    getTeamActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await teamsGetActivities(String(idOrKey), {
            page: page,
            pageSize: pageSize,
          })
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

    getTeamOfTeamsActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await teamsOfTeamsGetActivities(String(idOrKey), {
            page: page,
            pageSize: pageSize,
          })
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
  useGetTeamsQuery,
  useGetTeamDetailsQuery,
  useDeactivateTeamMutation,
  useDeactivateTeamOfTeamsMutation,
  useGetTeamOptionsQuery,
  useGetTeamBacklogQuery,
  useGetTeamBacklogHealthQuery,
  useGetTeamWorkDistributionQuery,
  useGetTeamWorkItemsQuery,
  useGetTeamDependenciesQuery,
  useGetTeamSprintsQuery,
  useGetTeamSprintMetricsQuery,
  useGetTeamCadenceMetricsQuery,
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
  useGetTeamOfTeamsDependenciesQuery,
  useGetTeamDependencyAnalysisQuery,
  // Operating Models
  useGetOperatingModelDefaultsQuery,
  useGetTeamOperatingModelQuery,
  useGetTeamOperatingModelAsOfQuery,
  useGetTeamOperatingModelsQuery,
  useGetTeamHasEverBeenScrumQuery,
  useSetTeamOperatingModelMutation,
  useUpdateTeamOperatingModelMutation,
  useDeleteTeamOperatingModelMutation,
  useGetTeamOperatingModelsForTeamsQuery,
  useGetTeamOfTeamsOperatingModelsQuery,
  useGetTeamOfTeamsOperatingModelQuery,
  useSetTeamOfTeamsOperatingModelMutation,
  useUpdateTeamOfTeamsOperatingModelMutation,
  useDeleteTeamOfTeamsOperatingModelMutation,
  useGetTeamOfTeamsDetailsQuery,
  useCreateTeamMutation,
  useUpdateTeamMutation,
  useGetTeamActivitiesQuery,
  useLazyGetTeamActivitiesQuery,
  useGetTeamOfTeamsActivitiesQuery,
  useLazyGetTeamOfTeamsActivitiesQuery,
} = teamApi
