import { homeContext, HomeContextOverrides } from './home-context-test-utils'
import { involvementPlacement } from './home-involvement'
import { HomeWidgets } from './home-widgets'
import { arrangeWidgets, attentionTiles, WidgetAccess } from './widget-registry'

// The widget modules load RTK Query hooks; these tests read only the registry entries.
vi.mock('./widgets/following-widget', () => ({ default: () => null }))
vi.mock('./widgets/my-projects-widget', () => ({
  default: () => null,
  OverdueTasksTile: () => null,
  DueThisWeekTile: () => null,
}))
vi.mock('./widgets/my-risks-widget', () => ({
  default: () => null,
  RiskFollowUpsTile: () => null,
}))
vi.mock('./widgets/my-team-sprints-widget', () => ({ default: () => null }))
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

  it('fills the strip with the four tiles of the default briefing', () => {
    // Arrange / Act
    const tiles = attentionTiles(arrangeWidgets(HomeWidgets, everything))

    // Assert
    expect(tiles.map((t) => t.id)).toEqual([
      'unread-notifications',
      'overdue-tasks',
      'tasks-due-this-week',
      'risk-follow-ups',
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
  const columns = arrangeWidgets(
    HomeWidgets,
    everything,
    involvementPlacement(HomeWidgets, homeContext(overrides)),
  )
  return {
    main: columns.main.map((w) => w.id),
    side: columns.side.map((w) => w.id),
    tiles: attentionTiles(columns).map((t) => t.id),
  }
}

describe('HomeWidgets involvement rules', () => {
  it('puts a team member’s sprints first, above areas they are not in', () => {
    // Arrange / Act
    const layout = defaultFor({ teams: { teamCount: 2 } })

    // Assert
    expect(layout.main).toEqual([
      'my-team-sprints',
      'whats-changed',
      'my-projects',
      'my-risks',
    ])
    expect(layout.side).toEqual(['planning-intervals', 'following'])
  })

  it('puts a project leader’s projects and their task counts first', () => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 3, ownerCount: 1 } })

    // Assert
    expect(layout.main[0]).toBe('my-projects')
    expect(layout.tiles.slice(0, 2)).toEqual([
      'overdue-tasks',
      'tasks-due-this-week',
    ])
  })

  it('ranks a project member below a project leader but above shared widgets', () => {
    // Arrange / Act
    const layout = defaultFor({ projects: { totalCount: 1, memberCount: 1 } })

    // Assert
    expect(layout.main).toEqual(['my-projects', 'whats-changed', 'my-risks'])
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
      'my-team-sprints',
      'my-risks',
      'whats-changed',
    ])
    expect(layout.side).toEqual(['planning-intervals', 'following'])
  })

  it('keeps sprints in the side column for a member of a team of teams only', () => {
    // Arrange / Act
    const layout = defaultFor({ teams: { teamOfTeamsCount: 1 } })

    // Assert
    expect(layout.side).toEqual([
      'planning-intervals',
      'following',
      'my-team-sprints',
    ])
  })
})
