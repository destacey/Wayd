import {
  arrangeWidgets,
  attentionTiles,
  HomeWidget,
  isWidgetAvailable,
  WidgetAccess,
} from './widget-registry'

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

const tile = (id: string) => ({ id, Tile: Nothing })

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

  it('needs the feature flag the widget names to be on', () => {
    // Arrange
    const subject = widget({ requires: { featureFlag: 'following' } })
    const isFeatureEnabled = jest.fn((name: string) => name !== 'following')

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

  it('shows at most four', () => {
    // Arrange
    const columns = {
      main: [
        widget({ id: 'a', tiles: [tile('a1'), tile('a2'), tile('a3')] }),
        widget({ id: 'b', tiles: [tile('b1'), tile('b2')] }),
      ],
      side: [],
    }

    // Act
    const tiles = attentionTiles(columns)

    // Assert
    expect(tiles.map((t) => t.id)).toEqual(['a1', 'a2', 'a3', 'b1'])
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
