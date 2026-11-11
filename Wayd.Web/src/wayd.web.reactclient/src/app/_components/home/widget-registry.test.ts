import {
  arrangeWidgets,
  attentionTiles,
  visibleAttentionTiles,
  HomeWidget,
  isWidgetAvailable,
  WidgetAccess,
} from './widget-registry'

import { homeContext } from './home-context-test-utils'

const Nothing = () => null

const widget = (overrides: Partial<HomeWidget>): HomeWidget => ({
  id: 'widget',
  title: 'Widget',
  description: 'A widget.',
  defaultColumn: 'main',
  defaultOrder: 10,
  requires: {},
  Component: Nothing,
  ...overrides,
})

const access = (overrides: Partial<WidgetAccess> = {}): WidgetAccess => ({
  hasPermissionClaim: () => true,
  isFeatureEnabled: () => true,
  hasLinkedEmployee: true,
  ...overrides,
})

const tile = (id: string, priority = 0) => ({ id, priority, Tile: Nothing })

describe('isWidgetAvailable', () => {
  it('shows a widget with no requirements to anyone', () => {
    // Arrange
    const subject = widget({ requires: {} })

    // Act
    const result = isWidgetAvailable(
      subject,
      access({
        hasPermissionClaim: () => false,
        isFeatureEnabled: () => false,
        hasLinkedEmployee: false,
      }),
    )

    // Assert
    expect(result).toBe(true)
  })

  it('needs every permission claim the widget names', () => {
    // Arrange
    const subject = widget({
      requires: {
        permissions: ['Permissions.Projects.View', 'Permissions.Risks.View'],
      },
    })
    const holdsProjectsOnly = access({
      hasPermissionClaim: (claim) => claim === 'Permissions.Projects.View',
    })

    // Act
    const withOne = isWidgetAvailable(subject, holdsProjectsOnly)
    const withBoth = isWidgetAvailable(subject, access())

    // Assert
    expect(withOne).toBe(false)
    expect(withBoth).toBe(true)
  })

  it('needs the involvement the widget names, read from the home context', () => {
    // Arrange
    const subject = widget({
      requires: {
        involved: ({ projects }) => projects.managerCount > 0,
      },
    })

    // Act
    const asManager = isWidgetAvailable(
      subject,
      access({ context: homeContext({ projects: { managerCount: 1 } }) }),
    )
    const asMember = isWidgetAvailable(
      subject,
      access({ context: homeContext({ projects: { memberCount: 1 } }) }),
    )
    const unknown = isWidgetAvailable(subject, access())

    // Assert
    expect(asManager).toBe(true)
    expect(asMember).toBe(false)
    expect(unknown).toBe(false)
  })

  it('needs the feature flag the widget names to be on', () => {
    // Arrange
    const subject = widget({ requires: { featureFlag: 'following' } })
    const isFeatureEnabled = vi.fn((name: string) => name !== 'following')

    // Act
    const result = isWidgetAvailable(subject, access({ isFeatureEnabled }))

    // Assert
    expect(result).toBe(false)
    expect(isFeatureEnabled).toHaveBeenCalledWith('following')
  })

  it('needs a linked employee only when the widget asks for one', () => {
    // Arrange
    const personal = widget({ requires: { linkedEmployee: true } })
    const shared = widget({ requires: { linkedEmployee: false } })
    const unlinked = access({ hasLinkedEmployee: false })

    // Act
    const personalShown = isWidgetAvailable(personal, unlinked)
    const sharedShown = isWidgetAvailable(shared, unlinked)

    // Assert
    expect(personalShown).toBe(false)
    expect(sharedShown).toBe(true)
  })
})

describe('arrangeWidgets', () => {
  it('splits the available widgets into their columns in default order', () => {
    // Arrange
    const widgets = [
      widget({ id: 'main-late', defaultOrder: 30 }),
      widget({ id: 'side-only', defaultColumn: 'side', defaultOrder: 5 }),
      widget({ id: 'main-early', defaultOrder: 10 }),
      widget({ id: 'main-mid', defaultOrder: 20 }),
    ]

    // Act
    const columns = arrangeWidgets(widgets, access())

    // Assert
    expect(columns.main.map((w) => w.id)).toEqual([
      'main-early',
      'main-mid',
      'main-late',
    ])
    expect(columns.side.map((w) => w.id)).toEqual(['side-only'])
  })

  it('leaves out a widget whose requirements fail', () => {
    // Arrange
    const widgets = [
      widget({ id: 'open' }),
      widget({ id: 'personal', requires: { linkedEmployee: true } }),
    ]

    // Act
    const columns = arrangeWidgets(
      widgets,
      access({ hasLinkedEmployee: false }),
    )

    // Assert
    expect(columns.main.map((w) => w.id)).toEqual(['open'])
  })

  it('places widgets where a given placement says, over their defaults', () => {
    // Arrange
    const widgets = [
      widget({ id: 'first', defaultOrder: 10 }),
      widget({ id: 'second', defaultOrder: 20 }),
      widget({ id: 'moved', defaultOrder: 30 }),
    ]
    const saved = {
      first: { column: 'main' as const, order: 2 },
      second: { column: 'main' as const, order: 1 },
      moved: { column: 'side' as const, order: 1 },
    }

    // Act
    const columns = arrangeWidgets(
      widgets,
      access(),
      (w) => saved[w.id as keyof typeof saved],
    )

    // Assert
    expect(columns.main.map((w) => w.id)).toEqual(['second', 'first'])
    expect(columns.side.map((w) => w.id)).toEqual(['moved'])
  })
})

describe('attentionTiles', () => {
  it('takes tiles in widget order, main column first', () => {
    // Arrange
    const columns = {
      main: [widget({ id: 'a', tiles: [tile('a1'), tile('a2')] })],
      side: [widget({ id: 'b', tiles: [tile('b1')] })],
    }

    // Act
    const tiles = attentionTiles(columns)

    // Assert
    expect(tiles.map((t) => t.id)).toEqual(['a1', 'a2', 'b1'])
  })

  it('puts a higher priority ahead of page order', () => {
    // Arrange
    const columns = {
      main: [widget({ id: 'a', tiles: [tile('low', 10), tile('tied', 50)] })],
      side: [widget({ id: 'b', tiles: [tile('high', 90), tile('later', 50)] })],
    }

    // Act
    const tiles = attentionTiles(columns)

    // Assert
    expect(tiles.map((t) => t.id)).toEqual(['high', 'tied', 'later', 'low'])
  })

  it('drops the tiles of a widget that is not shown', () => {
    // Arrange
    const widgets = [
      widget({ id: 'shown', tiles: [tile('kept')] }),
      widget({
        id: 'hidden',
        requires: { linkedEmployee: true },
        tiles: [tile('dropped')],
      }),
    ]

    // Act
    const tiles = attentionTiles(
      arrangeWidgets(widgets, access({ hasLinkedEmployee: false })),
    )

    // Assert
    expect(tiles.map((t) => t.id)).toEqual(['kept'])
  })
})

describe('visibleAttentionTiles', () => {
  const tiles = ['a', 'b', 'c', 'd', 'e', 'f'].map((id) => tile(id))
  const counted = (value: number | undefined) => ({ value, loading: false })

  it('shows at most four', () => {
    // Arrange
    const reports = Object.fromEntries(tiles.map((t) => [t.id, counted(1)]))

    // Act
    const visible = visibleAttentionTiles(tiles, reports)

    // Assert
    expect([...visible]).toEqual(['a', 'b', 'c', 'd'])
  })

  it('gives a zero’s slot to the next tile', () => {
    // Arrange
    const reports = {
      a: counted(0),
      b: counted(2),
      c: counted(0),
      d: counted(1),
      e: counted(3),
      f: counted(4),
    }

    // Act
    const visible = visibleAttentionTiles(tiles, reports)

    // Assert
    expect([...visible]).toEqual(['b', 'd', 'e', 'f'])
  })

  it('holds a slot for a tile still loading or not yet reported', () => {
    // Arrange
    const reports = { a: { value: undefined, loading: true }, b: counted(0) }

    // Act
    const visible = visibleAttentionTiles(tiles, reports)

    // Assert
    expect([...visible]).toEqual(['a', 'c', 'd', 'e'])
  })

  it('keeps a tile whose count failed, since a dash is not good news', () => {
    // Arrange
    const reports = Object.fromEntries(tiles.map((t) => [t.id, counted(0)]))
    reports.c = counted(undefined)

    // Act
    const visible = visibleAttentionTiles(tiles, reports)

    // Assert
    expect([...visible]).toEqual(['c'])
  })

  it('shows nothing when every count is zero', () => {
    // Arrange
    const reports = Object.fromEntries(tiles.map((t) => [t.id, counted(0)]))

    // Act
    const visible = visibleAttentionTiles(tiles, reports)

    // Assert
    expect(visible.size).toBe(0)
  })
})
