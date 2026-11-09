import { HomeContextResponse } from '@/src/services/wayd-api'

/** Overrides for {@link homeContext}, each area's counts merged over zeros. */
export interface HomeContextOverrides {
  teams?: Partial<HomeContextResponse['teams']>
  projects?: Partial<HomeContextResponse['projects']>
  intake?: Partial<HomeContextResponse['intake']>
  assignedRiskCount?: number
  teamProductCount?: number
}

/** A linked caller's home context: involved in nothing but what the overrides add. */
export const homeContext = (
  overrides: HomeContextOverrides = {},
): HomeContextResponse => ({
  hasLinkedEmployee: true,
  teams: { teamCount: 0, teamOfTeamsCount: 0, ...overrides.teams },
  projects: {
    totalCount: 0,
    sponsorCount: 0,
    ownerCount: 0,
    managerCount: 0,
    memberCount: 0,
    assigneeCount: 0,
    ...overrides.projects,
  },
  intake: {
    deskCount: 0,
    adminDeskCount: 0,
    agentDeskCount: 0,
    openRequestsRaisedCount: 0,
    ...overrides.intake,
  },
  assignedRiskCount: overrides.assignedRiskCount ?? 0,
  teamProductCount: overrides.teamProductCount ?? 0,
})
