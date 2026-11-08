import { HomeWidgets } from './home-widgets'
import { arrangeWidgets, attentionTiles, WidgetAccess } from './widget-registry'

// The widget modules load RTK Query hooks; these tests read only the registry entries.
jest.mock('./widgets/following-widget', () => () => null)
jest.mock('./widgets/my-projects-widget', () => ({
  __esModule: true,
  default: () => null,
  OverdueTasksTile: () => null,
  DueThisWeekTile: () => null,
}))
jest.mock('./widgets/my-risks-widget', () => ({
  __esModule: true,
  default: () => null,
  RiskFollowUpsTile: () => null,
}))
jest.mock('./widgets/my-team-sprints-widget', () => () => null)
jest.mock('./widgets/planning-intervals-widget', () => () => null)
jest.mock('./widgets/whats-changed-widget', () => ({
  __esModule: true,
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
