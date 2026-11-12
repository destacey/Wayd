import { homeContext, HomeContextOverrides } from './home-context-test-utils'
import { involvementPlacement } from './home-involvement'
import { HomeWidgets } from './home-widgets'
import { arrangeWidgets, attentionTiles, WidgetAccess } from './widget-registry'

// The widget modules load RTK Query hooks; these tests read only the registry entries.
vi.mock('./widgets/desk-queue-widget', () => ({
  default: () => null,
  RequestsAssignedToMeTile: () => null,
  UnassignedRequestsTile: () => null,
}))
vi.mock('./widgets/following-widget', () => ({ default: () => null }))
vi.mock('./widgets/my-requests-widget', () => ({ default: () => null }))
vi.mock('./widgets/my-projects-widget', () => ({ default: () => null }))
vi.mock('./widgets/my-project-tasks-widget', () => ({
  default: () => null,
  OverdueTasksTile: () => null,
  DueThisWeekTile: () => null,
}))
vi.mock('./widgets/project-health-checks-due-widget', () => ({
  default: () => null,
  ProjectHealthChecksDueTile: () => null,
}))
vi.mock('./widgets/my-deliverables-widget', () => ({ default: () => null }))
vi.mock('./widgets/my-work-items-widget', () => ({ default: () => null }))
vi.mock('./widgets/my-risks-widget', () => ({
  default: () => null,
  RiskFollowUpsTile: () => null,
}))
vi.mock('./widgets/my-teams-widget', () => ({ default: () => null }))
vi.mock('./widgets/my-teams-tile', () => ({
  CrossTeamDependenciesTile: () => null,
  DependenciesAtRiskTile: () => null,
  SprintsToCompleteTile: () => null,
}))
vi.mock('./widgets/planning-intervals-widget', () => ({ default: () => null }))
vi.mock('./widgets/whats-changed-widget', () => ({
  default: () => null,
  UnreadNotificationsTile: () => null,
}))

const everything: WidgetAccess = {
  hasPermissionClaim: () => true,
  isFeatureEnabled: () => true,
  hasLinkedEmployee: true,
}

/** Every permission and flag, as a project manager with tasks: everything a widget can require. */
const everythingAsAManager: WidgetAccess = {
  ...everything,
  context: homeContext({
    projects: { totalCount: 1, managerCount: 1, assigneeCount: 1 },
  }),
}

/** The tiles of widgets shown only to some people: project task assignees, owners and managers. */
const projectDutyTiles = [
  'overdue-tasks',
  'tasks-due-this-week',
  'project-health-checks-due',
]

describe('HomeWidgets', () => {
  it('gives every widget a unique id, since saved layouts refer to them', () => {
    // Arrange
    const ids = HomeWidgets.map((w) => w.id)

    // Act
    const unique = new Set(ids)

    // Assert
    expect(unique.size).toBe(ids.length)
  })

  it('shows an unlinked account only the widgets that need no employee', () => {
    // Arrange
    const unlinked = { ...everything, hasLinkedEmployee: false }

    // Act
    const columns = arrangeWidgets(HomeWidgets, unlinked)

    // Assert
    expect([...columns.main, ...columns.side].map((w) => w.id)).toEqual([
      'planning-intervals',
    ])
    expect(attentionTiles(columns)).toEqual([])
  })

  it('offers every tile of the default briefing, most urgent first', () => {
    // Arrange / Act
    const tiles = attentionTiles(
      arrangeWidgets(HomeWidgets, everythingAsAManager),
    )

    // Assert
    expect(tiles.map((t) => t.id)).toEqual([
      'overdue-tasks',
      'sprints-to-complete',
      'project-health-checks-due',
      'risk-follow-ups',
      'cross-team-dependencies',
      'dependencies-at-risk',
      'unassigned-requests',
      'tasks-due-this-week',
      'requests-assigned-to-me',
      'unread-notifications',
    ])
  })

  it('hides the notification and follow widgets while the Following flag is off', () => {
    // Arrange
    const flagOff = { ...everything, isFeatureEnabled: () => false }

    // Act
    const columns = arrangeWidgets(HomeWidgets, flagOff)

    // Assert
    const ids = [...columns.main, ...columns.side].map((w) => w.id)
    expect(ids).not.toContain('whats-changed')
    expect(ids).not.toContain('following')
    expect(ids).toContain('my-projects')
  })
})

const defaultFor = (overrides: HomeContextOverrides) => {
  const context = homeContext(overrides)
  const columns = arrangeWidgets(
    HomeWidgets,
    { ...everything, context },
    involvementPlacement(HomeWidgets, context),
  )
  return {
    main: columns.main.map((w) => w.id),
    side: columns.side.map((w) => w.id),
    tiles: attentionTiles(columns).map((t) => t.id),
  }
}

describe('HomeWidgets involvement rules', () => {
  it('puts a team member’s teams and work items first, above areas they are not in', () => {
    // Arrange / Act
    const layout = defaultFor({ teams: { teamCount: 2 } })

    // Assert
    expect(layout.main).toEqual([
      'my-teams',
      'my-work-items',
      'whats-changed',
      'my-projects',
      'my-risks',
      'desk-queue',
    ])
    expect(layout.side).toEqual([
      'planning-intervals',
      'following',
      'my-requests',
      'my-deliverables',
    ])
  })

  it('puts a project leader’s projects first', () => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 3, ownerCount: 1 } })

    // Assert
    expect(layout.main[0]).toBe('my-projects')
  })

  it.each([
    ['a project leader', { projects: { totalCount: 3, ownerCount: 1 } }],
    ['a team member', { teams: { teamCount: 2 } }],
    ['someone involved in nothing', {}],
  ] as [string, HomeContextOverrides][])(
    'orders the tiles by priority alone for %s',
    (_, overrides) => {
      // Arrange / Act
      const layout = defaultFor(overrides)

      // Assert — the project task and health check tiles belong to people who hold those duties.
      expect(layout.tiles.filter((t) => !projectDutyTiles.includes(t))).toEqual(
        [
          'sprints-to-complete',
          'risk-follow-ups',
          'cross-team-dependencies',
          'dependencies-at-risk',
          'unassigned-requests',
          'requests-assigned-to-me',
          'unread-notifications',
        ],
      )
    },
  )

  it('puts a task assignee’s project tasks first, with their tiles', () => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 1, assigneeCount: 1 } })

    // Assert
    expect(layout.main[0]).toBe('my-project-tasks')
    expect(layout.tiles).toEqual(
      expect.arrayContaining(['overdue-tasks', 'tasks-due-this-week']),
    )
  })

  it.each([
    ['a sponsor', { sponsorCount: 1 }],
    ['an owner', { ownerCount: 1 }],
    ['a manager', { managerCount: 1 }],
    ['a member', { memberCount: 1 }],
  ])(
    'shows project tasks, and their tiles, to %s only once they are assigned one',
    (_, role) => {
      // Arrange / Act
      const layout = defaultFor({ projects: { totalCount: 1, ...role } })

      // Assert
      expect([...layout.main, ...layout.side]).not.toContain('my-project-tasks')
      expect(layout.tiles).not.toContain('overdue-tasks')
      expect(layout.tiles).not.toContain('tasks-due-this-week')
    },
  )

  it.each([
    ['a sponsor', { sponsorCount: 1 }],
    ['a member', { memberCount: 1 }],
    ['a task assignee', { assigneeCount: 1 }],
  ])('shows health checks due neither to %s nor in their tiles', (_, role) => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 1, ...role } })

    // Assert
    expect([...layout.main, ...layout.side]).not.toContain(
      'project-health-checks-due',
    )
    expect(layout.tiles).not.toContain('project-health-checks-due')
  })

  it.each([
    ['an owner', { ownerCount: 1 }],
    ['a manager', { managerCount: 1 }],
  ])('shows health checks due, and its tile, to %s', (_, role) => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 1, ...role } })

    // Assert
    expect(layout.main).toContain('project-health-checks-due')
    expect(layout.tiles).toContain('project-health-checks-due')
  })

  it('hides health checks due until the home context has loaded', () => {
    // Arrange / Act
    const columns = arrangeWidgets(HomeWidgets, everything)

    // Assert
    expect([...columns.main, ...columns.side].map((w) => w.id)).not.toContain(
      'project-health-checks-due',
    )
  })

  it('brings health checks due into the main column for a project owner or manager', () => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 1, managerCount: 1 } })

    // Assert
    expect(layout.main.slice(0, 3)).toEqual([
      'my-projects',
      'project-health-checks-due',
      'my-deliverables',
    ])
  })

  it('ranks a project member below a project leader but above shared widgets', () => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 1, memberCount: 1 } })

    // Assert
    expect(layout.main).toEqual([
      'my-projects',
      'whats-changed',
      'my-work-items',
      'my-risks',
      'desk-queue',
    ])
  })

  it('interleaves a leader who is also on a team and holds risks', () => {
    // Arrange / Act
    const layout = defaultFor({
      teams: { teamCount: 1 },
      projects: { totalCount: 2, managerCount: 2 },
      assignedRiskCount: 1,
    })

    // Assert
    expect(layout.main).toEqual([
      'my-projects',
      'my-teams',
      'my-work-items',
      'project-health-checks-due',
      'my-deliverables',
      'my-risks',
      'whats-changed',
      'desk-queue',
    ])
    expect(layout.side).toEqual([
      'planning-intervals',
      'following',
      'my-requests',
    ])
  })

  it('puts My teams first for a member of a team of teams only, whose rollup it shows', () => {
    // Arrange / Act
    const layout = defaultFor({ teams: { teamOfTeamsCount: 1 } })

    // Assert
    // Their own work items are not raised: the rollup is about teams beneath them.
    expect(layout.main.slice(0, 3)).toEqual([
      'my-teams',
      'whats-changed',
      'my-work-items',
    ])
    expect(layout.side).toEqual([
      'planning-intervals',
      'following',
      'my-requests',
      'my-deliverables',
    ])
  })

  it('ranks work items as shared for someone on no team', () => {
    // Arrange / Act
    const layout = defaultFor({})

    // Assert
    expect(layout.main.slice(0, 2)).toEqual(['whats-changed', 'my-work-items'])
  })

  it('puts the desk queue first for someone who works an intake desk', () => {
    // Arrange / Act
    const layout = defaultFor({ intake: { deskCount: 1, agentDeskCount: 1 } })

    // Assert
    expect(layout.main[0]).toBe('desk-queue')
  })

  it('puts My requests first in the side column for someone with an open request', () => {
    // Arrange / Act
    const layout = defaultFor({ intake: { openRequestsRaisedCount: 2 } })

    // Assert
    expect(layout.side[0]).toBe('my-requests')
  })
})
