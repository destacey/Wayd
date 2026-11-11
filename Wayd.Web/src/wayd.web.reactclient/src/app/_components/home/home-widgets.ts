import { HomeContextResponse } from '@/src/services/wayd-api'
import { lazy } from 'react'
import { Involvement } from './home-involvement'
import { HomeWidget } from './widget-registry'
import DeskQueueWidget, {
  RequestsAssignedToMeTile,
  UnassignedRequestsTile,
} from './widgets/desk-queue-widget'
import FollowingWidget from './widgets/following-widget'
import MyProjectsWidget, {
  DueThisWeekTile,
  OverdueTasksTile,
} from './widgets/my-projects-widget'
import MyRequestsWidget from './widgets/my-requests-widget'
import MyRisksWidget, { RiskFollowUpsTile } from './widgets/my-risks-widget'
import PlanningIntervalsWidget from './widgets/planning-intervals-widget'
import WhatsChangedWidget, {
  UnreadNotificationsTile,
} from './widgets/whats-changed-widget'

import {
  CrossTeamDependenciesTile,
  DependenciesAtRiskTile,
  SprintsToCompleteTile,
} from './widgets/my-teams-tile'

// Loaded after the page: each sprint draws a burn-up chart, and the charting
// library is the heaviest code the home page would otherwise carry up front.
const MyTeamsWidget = lazy(() => import('./widgets/my-teams-widget'))

/**
 * How urgent each attention tile is when above zero: the strip shows the
 * highest first. Work already late outranks work coming due, and both outrank
 * news, which waits until read.
 */
export const TilePriority = {
  OverdueTasks: 100,
  SprintsToComplete: 90,
  RiskFollowUps: 80,
  CrossTeamDependencies: 75,
  DependenciesAtRisk: 70,
  UnassignedRequests: 65,
  TasksDueThisWeek: 60,
  RequestsAssignedToMe: 50,
  UnreadNotifications: 40,
} as const

/** Leading a project (sponsor, owner or manager) outranks working on one. */
const projectInvolvement = ({ projects }: HomeContextResponse) => {
  if (projects.sponsorCount + projects.ownerCount + projects.managerCount > 0)
    return Involvement.Leads
  return projects.totalCount > 0 ? Involvement.Involved : Involvement.None
}

const teamCount = ({ teams }: HomeContextResponse) =>
  teams.teamCount + teams.teamOfTeamsCount

/**
 * Every widget the home page can show. Ids are stored in users' saved layouts,
 * so an id is never renamed or reused once shipped.
 */
export const HomeWidgets: HomeWidget[] = [
  {
    id: 'whats-changed',
    title: "What's changed",
    description:
      'The latest changes to the records you follow or work on, from your notifications.',
    defaultColumn: 'main',
    defaultOrder: 10,
    requires: { featureFlag: 'following', linkedEmployee: true },
    Component: WhatsChangedWidget,
    tiles: [
      {
        id: 'unread-notifications',
        priority: TilePriority.UnreadNotifications,
        Tile: UnreadNotificationsTile,
      },
    ],
  },
  {
    id: 'my-projects',
    title: 'My projects',
    description:
      'How many approved and active projects you hold a role on, by role.',
    defaultColumn: 'main',
    defaultOrder: 20,
    requires: {
      permissions: ['Permissions.Projects.View'],
      linkedEmployee: true,
    },
    Component: MyProjectsWidget,
    involvement: projectInvolvement,
    tiles: [
      {
        id: 'overdue-tasks',
        priority: TilePriority.OverdueTasks,
        Tile: OverdueTasksTile,
      },
      {
        id: 'tasks-due-this-week',
        priority: TilePriority.TasksDueThisWeek,
        Tile: DueThisWeekTile,
      },
    ],
  },
  {
    id: 'my-risks',
    title: 'My risks',
    description: 'The open risks assigned to you, soonest follow-up first.',
    defaultColumn: 'main',
    defaultOrder: 30,
    requires: { permissions: ['Permissions.Risks.View'], linkedEmployee: true },
    Component: MyRisksWidget,
    involvement: (context) =>
      context.assignedRiskCount > 0 ? Involvement.Involved : Involvement.None,
    tiles: [
      {
        id: 'risk-follow-ups',
        priority: TilePriority.RiskFollowUps,
        Tile: RiskFollowUpsTile,
      },
    ],
  },
  {
    id: 'planning-intervals',
    title: 'Planning intervals',
    description: `Planning intervals running now or starting in the next two weeks.`,
    // Shared context rather than the caller's own work: it sits above the team
    // sprints that run inside it, leaving the main column for what needs action.
    defaultColumn: 'side',
    defaultOrder: 10,
    requires: { permissions: ['Permissions.PlanningIntervals.View'] },
    Component: PlanningIntervalsWidget,
    // Teams plan in planning intervals, so they are context for team members.
    involvement: (context) =>
      teamCount(context) > 0 ? Involvement.Involved : Involvement.None,
  },
  {
    id: 'my-teams',
    title: 'My teams',
    description:
      "Each team you are a member of: a Scrum team's active sprint or a Kanban team's flow, with its dependencies and risks. Each team of teams you belong to directly is one card, its teams needing attention first.",
    defaultColumn: 'side',
    defaultOrder: 20,
    requires: { linkedEmployee: true },
    Component: MyTeamsWidget,
    // A team member's team, or a team of teams they answer for, is their day-to-day work.
    involvement: (context) =>
      teamCount(context) > 0 ? Involvement.Leads : Involvement.None,
    tiles: [
      {
        id: 'sprints-to-complete',
        priority: TilePriority.SprintsToComplete,
        Tile: SprintsToCompleteTile,
      },
      {
        id: 'cross-team-dependencies',
        priority: TilePriority.CrossTeamDependencies,
        Tile: CrossTeamDependenciesTile,
      },
      {
        id: 'dependencies-at-risk',
        priority: TilePriority.DependenciesAtRisk,
        Tile: DependenciesAtRiskTile,
      },
    ],
  },
  {
    id: 'desk-queue',
    title: 'Desk queue',
    description:
      'What waits on the intake desks you work: open and untriaged requests on each desk, the requests assigned to you, and those you take part in.',
    defaultColumn: 'main',
    defaultOrder: 40,
    requires: { featureFlag: 'demand-management', linkedEmployee: true },
    Component: DeskQueueWidget,
    // Working a desk is day-to-day work.
    involvement: ({ intake }) =>
      intake.deskCount > 0 ? Involvement.Leads : Involvement.None,
    tiles: [
      {
        id: 'unassigned-requests',
        priority: TilePriority.UnassignedRequests,
        Tile: UnassignedRequestsTile,
      },
      {
        id: 'requests-assigned-to-me',
        priority: TilePriority.RequestsAssignedToMe,
        Tile: RequestsAssignedToMeTile,
      },
    ],
  },
  {
    id: 'my-requests',
    title: 'My requests',
    description:
      'The intake requests you raised that are open or closed in the last 30 days, with their status and the projects and work items they became.',
    defaultColumn: 'side',
    defaultOrder: 25,
    requires: { featureFlag: 'demand-management', linkedEmployee: true },
    Component: MyRequestsWidget,
    involvement: ({ intake }) =>
      intake.openRequestsRaisedCount > 0
        ? Involvement.Involved
        : Involvement.None,
  },
  {
    id: 'following',
    title: 'Following',
    description: 'The records you follow, most recently changed first.',
    defaultColumn: 'side',
    defaultOrder: 30,
    requires: { featureFlag: 'following', linkedEmployee: true },
    Component: FollowingWidget,
  },
]
