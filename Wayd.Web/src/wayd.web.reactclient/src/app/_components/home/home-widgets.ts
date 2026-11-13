import { HomeContextResponse } from '@/src/services/wayd-api'
import { lazy } from 'react'
import { Involvement } from './home-involvement'
import { HomeWidget } from './widget-registry'
import { emptySetting, rowsSetting } from './widget-settings'
import DeskQueueWidget, {
  DESK_QUEUE_LIMIT,
  RequestsAssignedToMeTile,
  UnassignedRequestsTile,
} from './widgets/desk-queue-widget'
import FollowingWidget, { FOLLOWING_COUNT } from './widgets/following-widget'
import ProjectHealthChecksDueWidget, {
  HEALTH_CHECKS_DUE_LIMIT,
  ProjectHealthChecksDueTile,
} from './widgets/project-health-checks-due-widget'
import MyDeliverablesWidget, {
  MY_DELIVERABLES_LIMIT,
} from './widgets/my-deliverables-widget'
import MyProjectTasksWidget, {
  DueThisWeekTile,
  MY_PROJECT_TASKS_LIMIT,
  OverdueTasksTile,
} from './widgets/my-project-tasks-widget'
import MyProjectsWidget from './widgets/my-projects-widget'
import MyRequestsWidget, {
  MY_REQUESTS_LIMIT,
} from './widgets/my-requests-widget'
import MyWorkItemsWidget, {
  MY_WORK_ITEMS_LIMIT,
} from './widgets/my-work-items-widget'
import MyRisksWidget, { RiskFollowUpsTile } from './widgets/my-risks-widget'
import MyRoadmapsWidget, {
  MY_ROADMAPS_LIMIT,
} from './widgets/my-roadmaps-widget'
import PlanningIntervalsWidget from './widgets/planning-intervals-widget'
import WhatsChangedWidget, {
  UnreadNotificationsTile,
  WHATS_CHANGED_COUNT,
} from './widgets/whats-changed-widget'

import {
  CrossTeamDependenciesTile,
  DependenciesAtRiskTile,
  SprintsToCompleteTile,
} from './widgets/my-teams-tile'

// Loaded after the page: each sprint draws a burn-up chart, and the charting
// library is the heaviest code the home page would otherwise carry up front.
const MyTeamsWidget = lazy(() => import('./widgets/my-teams-widget'))
// Its rollup is lighter, but it waits with My teams so neither team widget
// holds up the page.
const MyTeamsOfTeamsWidget = lazy(
  () => import('./widgets/my-teams-of-teams-widget'),
)

/**
 * How urgent each attention tile is when above zero: the strip shows the
 * highest first. Work already late outranks work coming due, and both outrank
 * news, which waits until read.
 */
export const TilePriority = {
  OverdueTasks: 100,
  SprintsToComplete: 90,
  ProjectHealthChecksDue: 85,
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
    settings: [rowsSetting(WHATS_CHANGED_COUNT), emptySetting],
    tiles: [
      {
        id: 'unread-notifications',
        priority: TilePriority.UnreadNotifications,
        Tile: UnreadNotificationsTile,
      },
    ],
  },
  {
    id: 'my-work-items',
    title: 'My work items',
    description:
      'The work items assigned to you that are in progress or proposed, in progress first, with how many you finished in the last week.',
    defaultColumn: 'main',
    // After My teams, so a team member sees their team before their own share of its work.
    defaultOrder: 22,
    requires: {
      permissions: ['Permissions.WorkItems.View'],
      linkedEmployee: true,
    },
    Component: MyWorkItemsWidget,
    settings: [rowsSetting(MY_WORK_ITEMS_LIMIT), emptySetting],
    // A team member's assigned work items are their day-to-day work; a member
    // of a team of teams alone holds a role above the work. The home context
    // counts no work items, so for anyone else it ranks as shared.
    involvement: ({ teams }) =>
      teams.teamCount > 0 ? Involvement.Leads : Involvement.Shared,
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
    settings: [emptySetting],
    involvement: projectInvolvement,
  },
  {
    id: 'my-project-tasks',
    title: 'My project tasks',
    description:
      'The open project plan tasks and milestones assigned to you on approved and active projects, overdue first.',
    defaultColumn: 'main',
    defaultOrder: 25,
    requires: {
      permissions: ['Permissions.Projects.View'],
      linkedEmployee: true,
      // Only someone assigned a task on an approved or active project has any to list.
      involved: ({ projects }) => projects.assigneeCount > 0,
    },
    Component: MyProjectTasksWidget,
    settings: [rowsSetting(MY_PROJECT_TASKS_LIMIT), emptySetting],
    // Assigned tasks are day-to-day work, and everyone it is shown to has some.
    involvement: () => Involvement.Leads,
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
    id: 'project-health-checks-due',
    title: 'Project health checks due',
    description:
      'Active projects you own or manage whose health check is missing, expired or expiring within a week.',
    defaultColumn: 'main',
    defaultOrder: 27,
    requires: {
      permissions: ['Permissions.Projects.View'],
      linkedEmployee: true,
      // Reporting health falls to a project's owners and managers; for anyone else it would only ever be empty.
      involved: ({ projects }) =>
        projects.ownerCount + projects.managerCount > 0,
    },
    Component: ProjectHealthChecksDueWidget,
    settings: [rowsSetting(HEALTH_CHECKS_DUE_LIMIT), emptySetting],
    // Everyone it is shown to leads a project.
    involvement: () => Involvement.Leads,
    tiles: [
      {
        id: 'project-health-checks-due',
        priority: TilePriority.ProjectHealthChecksDue,
        Tile: ProjectHealthChecksDueTile,
      },
    ],
  },
  {
    id: 'my-deliverables',
    title: 'Deliverables I own',
    description:
      'The open project deliverables you are the owner of, past target first.',
    defaultColumn: 'side',
    defaultOrder: 27,
    requires: {
      permissions: ['Permissions.Projects.View'],
      linkedEmployee: true,
    },
    Component: MyDeliverablesWidget,
    settings: [rowsSetting(MY_DELIVERABLES_LIMIT), emptySetting],
    // An owner need hold no role on the project, so this is a guess from the roles held.
    involvement: projectInvolvement,
  },
  {
    id: 'my-risks',
    title: 'My risks',
    description: 'The open risks assigned to you, soonest follow-up first.',
    defaultColumn: 'main',
    defaultOrder: 30,
    requires: { permissions: ['Permissions.Risks.View'], linkedEmployee: true },
    Component: MyRisksWidget,
    settings: [emptySetting],
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
    id: 'my-roadmaps',
    title: 'My roadmaps',
    description:
      'The active roadmaps you are an owner of, with their dates and visibility.',
    defaultColumn: 'side',
    defaultOrder: 35,
    requires: {
      permissions: ['Permissions.Roadmaps.View'],
      linkedEmployee: true,
      // Keeping a roadmap current falls to its owners; anyone else would only ever see it empty.
      involved: ({ ownedRoadmapCount }) => ownedRoadmapCount > 0,
    },
    Component: MyRoadmapsWidget,
    settings: [rowsSetting(MY_ROADMAPS_LIMIT), emptySetting],
    // Owning a roadmap is involvement, not day-to-day work, so it stays in the side column.
    involvement: () => Involvement.Involved,
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
    settings: [emptySetting],
    // Teams plan in planning intervals, so they are context for team members.
    involvement: (context) =>
      teamCount(context) > 0 ? Involvement.Involved : Involvement.None,
  },
  {
    id: 'my-teams',
    title: 'My teams',
    description:
      "Each team you are a member of: a Scrum team's active sprint or a Kanban team's flow, with its dependencies and risks.",
    defaultColumn: 'side',
    defaultOrder: 20,
    requires: { linkedEmployee: true },
    Component: MyTeamsWidget,
    settings: [emptySetting],
    // A team member's team is their day-to-day work.
    involvement: ({ teams }) =>
      teams.teamCount > 0 ? Involvement.Leads : Involvement.None,
    tiles: [
      {
        id: 'dependencies-at-risk',
        priority: TilePriority.DependenciesAtRisk,
        Tile: DependenciesAtRiskTile,
      },
    ],
  },
  {
    id: 'my-teams-of-teams',
    title: 'My teams of teams',
    description:
      'Each team of teams you are a member of directly, its teams needing attention first, with its cross-team dependencies and risks.',
    defaultColumn: 'side',
    defaultOrder: 21,
    requires: {
      linkedEmployee: true,
      // Membership of a team of teams is a role few hold; for anyone else it would only ever be empty.
      involved: ({ teams }) => teams.teamOfTeamsCount > 0,
    },
    Component: MyTeamsOfTeamsWidget,
    settings: [emptySetting],
    // A team of teams its member answers for is their day-to-day work, and everyone it is shown to has one.
    involvement: () => Involvement.Leads,
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
    // Its two request lists share the one count.
    settings: [
      rowsSetting(DESK_QUEUE_LIMIT, 'Requests per list'),
      emptySetting,
    ],
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
    settings: [rowsSetting(MY_REQUESTS_LIMIT), emptySetting],
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
    settings: [rowsSetting(FOLLOWING_COUNT), emptySetting],
  },
]
