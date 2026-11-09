import { HomeContextResponse } from '@/src/services/wayd-api'
import { lazy } from 'react'
import { Involvement } from './home-involvement'
import { HomeWidget } from './widget-registry'
import FollowingWidget from './widgets/following-widget'
import MyProjectsWidget, {
  DueThisWeekTile,
  OverdueTasksTile,
} from './widgets/my-projects-widget'
import MyRisksWidget, { RiskFollowUpsTile } from './widgets/my-risks-widget'
import PlanningIntervalsWidget from './widgets/planning-intervals-widget'
import WhatsChangedWidget, {
  UnreadNotificationsTile,
} from './widgets/whats-changed-widget'

// Loaded after the page: each sprint draws a burn-up chart, and the charting
// library is the heaviest code the home page would otherwise carry up front.
const MyTeamSprintsWidget = lazy(
  () => import('./widgets/my-team-sprints-widget'),
)

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
    tiles: [{ id: 'unread-notifications', Tile: UnreadNotificationsTile }],
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
      { id: 'overdue-tasks', Tile: OverdueTasksTile },
      { id: 'tasks-due-this-week', Tile: DueThisWeekTile },
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
    tiles: [{ id: 'risk-follow-ups', Tile: RiskFollowUpsTile }],
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
    id: 'my-team-sprints',
    title: 'My team sprints',
    description: 'The active sprint of each team you are a member of.',
    defaultColumn: 'side',
    defaultOrder: 20,
    requires: {
      permissions: [
        'Permissions.Employees.View',
        'Permissions.Iterations.View',
      ],
      linkedEmployee: true,
    },
    Component: MyTeamSprintsWidget,
    // A team member's sprint is their day-to-day work.
    involvement: (context) =>
      context.teams.teamCount > 0 ? Involvement.Leads : Involvement.None,
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
