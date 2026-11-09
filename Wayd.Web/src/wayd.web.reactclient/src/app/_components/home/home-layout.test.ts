import { HomeLayoutColumn } from '@/src/services/wayd-api'
import {
  arrangeFromLayout,
  fromSavedWidgets,
  HomeLayout,
  layoutToSave,
  moveWidget,
  resolveLayout,
  SavedWidget,
  setWidgetVisible,
  toSavedWidgets,
} from './home-layout'
import {
  attentionTiles,
  HomeWidget,
  WidgetAccess,
  WidgetColumn,
} from './widget-registry'

const Nothing = () => null

const widget = (
  id: string,
  defaultColumn: WidgetColumn,
  defaultOrder: number,
  overrides: Partial<HomeWidget> = {},
): HomeWidget => ({
  id,
  title: id,
  description: '',
  defaultColumn,
  defaultOrder,
  requires: {},
  Component: Nothing,
  ...overrides,
})

const tile = (id: string) => ({ id, Tile: Nothing })

const access = (overrides: Partial<WidgetAccess> = {}): WidgetAccess => ({
  hasPermissionClaim: () => true,
  isFeatureEnabled: () => true,
  hasLinkedEmployee: true,
  ...overrides,
})

const saved = (
  widgetId: string,
  column: WidgetColumn,
  visible = true,
): SavedWidget => ({ widgetId, column, visible })

const ids = (layout: HomeLayout) => ({
  main: layout.main.map((s) => s.id),
  side: layout.side.map((s) => s.id),
})

// a, b, c in main; x, y in side — by default order.
const registry = [
  widget('a', 'main', 10),
  widget('b', 'main', 20),
  widget('c', 'main', 30),
  widget('x', 'side', 10),
  widget('y', 'side', 20),
]

describe('resolveLayout', () => {
  it('uses the default columns and order when nothing is saved', () => {
    // Act
    const layout = resolveLayout(registry, access(), [])

    // Assert
    expect(ids(layout)).toEqual({ main: ['a', 'b', 'c'], side: ['x', 'y'] })
    expect([...layout.main, ...layout.side].every((s) => s.visible)).toBe(true)
  })

  it('keeps the saved columns, order and visibility', () => {
    // Arrange
    const layout = [
      saved('c', 'main'),
      saved('x', 'main', false),
      saved('a', 'main'),
      saved('y', 'side'),
      saved('b', 'side'),
    ]

    // Act
    const result = resolveLayout(registry, access(), layout)

    // Assert
    expect(ids(result)).toEqual({ main: ['c', 'x', 'a'], side: ['y', 'b'] })
    expect(result.main.find((s) => s.id === 'x')?.visible).toBe(false)
  })

  it('puts a widget the layout does not mention at its default position', () => {
    // Arrange: b and y arrived after the layout was saved.
    const layout = [saved('a', 'main'), saved('c', 'main'), saved('x', 'side')]

    // Act
    const result = resolveLayout(registry, access(), layout)

    // Assert
    expect(ids(result)).toEqual({ main: ['a', 'b', 'c'], side: ['x', 'y'] })
    expect(result.main[1]).toEqual({ id: 'b', visible: true })
  })

  it('puts a new widget ahead of the first saved widget with a later default', () => {
    // Arrange: the user moved c to the top; b is new.
    const layout = [saved('c', 'main'), saved('a', 'main')]
    const widgets = registry.filter((w) => w.defaultColumn === 'main')

    // Act
    const result = resolveLayout(widgets, access(), layout)

    // Assert
    expect(ids(result).main).toEqual(['b', 'c', 'a'])
  })

  it('skips a saved widget the account can no longer use', () => {
    // Arrange
    const widgets = [
      widget('a', 'main', 10),
      widget('flagged', 'main', 20, { requires: { featureFlag: 'following' } }),
    ]
    const layout = [saved('flagged', 'main'), saved('a', 'main')]

    // Act
    const result = resolveLayout(
      widgets,
      access({ isFeatureEnabled: () => false }),
      layout,
    )

    // Assert
    expect(ids(result)).toEqual({ main: ['a'], side: [] })
  })

  it('drops saved ids the registry no longer has, and repeats of an id', () => {
    // Arrange
    const layout = [
      saved('retired', 'main'),
      saved('b', 'side'),
      saved('b', 'main'),
    ]

    // Act
    const result = resolveLayout(registry, access(), layout)

    // Assert
    // x and y, unmentioned, join side by default order around b (default 20).
    expect(ids(result)).toEqual({ main: ['a', 'c'], side: ['x', 'b', 'y'] })
  })
})

describe('arrangeFromLayout', () => {
  it('shows only the visible widgets, in layout order', () => {
    // Arrange
    const layout: HomeLayout = {
      main: [
        { id: 'c', visible: true },
        { id: 'a', visible: false },
      ],
      side: [
        { id: 'b', visible: true },
        { id: 'x', visible: true },
      ],
    }

    // Act
    const columns = arrangeFromLayout(registry, access(), layout)

    // Assert
    expect(columns.main.map((w) => w.id)).toEqual(['c'])
    expect(columns.side.map((w) => w.id)).toEqual(['b', 'x'])
  })

  it('takes tiles only from shown widgets, following widget order', () => {
    // Arrange
    const widgets = [
      widget('a', 'main', 10, { tiles: [tile('a1')] }),
      widget('b', 'main', 20, { tiles: [tile('b1'), tile('b2')] }),
      widget('x', 'side', 10, { tiles: [tile('x1')] }),
      widget('y', 'side', 20, { tiles: [tile('y1'), tile('y2')] }),
    ]
    const layout: HomeLayout = {
      main: [
        { id: 'y', visible: true },
        { id: 'a', visible: false },
      ],
      side: [
        { id: 'x', visible: true },
        { id: 'b', visible: true },
      ],
    }

    // Act
    const tiles = attentionTiles(arrangeFromLayout(widgets, access(), layout))

    // Assert
    expect(tiles.map((t) => t.id)).toEqual(['y1', 'y2', 'x1', 'b1'])
  })
})

describe('layoutToSave', () => {
  it('saves the arranged widgets, main column first', () => {
    // Arrange
    const layout: HomeLayout = {
      main: [
        { id: 'b', visible: true },
        { id: 'a', visible: false },
      ],
      side: [{ id: 'c', visible: true }],
    }

    // Act
    const result = layoutToSave(layout, [], registry)

    // Assert
    expect(result).toEqual([
      saved('b', 'main'),
      saved('a', 'main', false),
      saved('c', 'side'),
    ])
  })

  it('keeps a saved widget the account cannot use, after the widget it followed', () => {
    // Arrange: y is saved hidden after a, but the account cannot use it now.
    const basis = [
      saved('a', 'main'),
      saved('y', 'side', false),
      saved('b', 'main'),
    ]
    const layout: HomeLayout = {
      main: [
        { id: 'b', visible: true },
        { id: 'a', visible: true },
      ],
      side: [],
    }

    // Act
    const result = layoutToSave(layout, basis, registry)

    // Assert
    expect(result).toEqual([
      saved('b', 'main'),
      saved('a', 'main'),
      saved('y', 'side', false),
    ])
  })

  it('keeps an unusable widget first when it led the saved layout', () => {
    // Arrange
    const basis = [saved('y', 'main'), saved('a', 'main')]
    const layout: HomeLayout = { main: [{ id: 'a', visible: true }], side: [] }

    // Act
    const result = layoutToSave(layout, basis, registry)

    // Assert
    expect(result).toEqual([saved('y', 'main'), saved('a', 'main')])
  })

  it('drops saved ids the registry no longer has', () => {
    // Arrange
    const basis = [saved('retired', 'main'), saved('a', 'main')]
    const layout: HomeLayout = { main: [{ id: 'a', visible: true }], side: [] }

    // Act
    const result = layoutToSave(layout, basis, registry)

    // Assert
    expect(result).toEqual([saved('a', 'main')])
  })

  it('returns an unusable widget to its place once access returns', () => {
    // Arrange: the flag is off while the user rearranges, then comes back on.
    const widgets = [
      widget('a', 'main', 10),
      widget('flagged', 'main', 20, { requires: { featureFlag: 'following' } }),
      widget('b', 'main', 30),
    ]
    const basis = [
      saved('a', 'main'),
      saved('flagged', 'main', false),
      saved('b', 'main'),
    ]
    const flagOff = access({ isFeatureEnabled: () => false })
    const edited = moveWidget(
      resolveLayout(widgets, flagOff, basis),
      'b',
      'side',
      0,
    )

    // Act
    const next = layoutToSave(edited, basis, widgets)
    const restored = resolveLayout(widgets, access(), next)

    // Assert
    expect(ids(restored)).toEqual({ main: ['a', 'flagged'], side: ['b'] })
    expect(restored.main[1].visible).toBe(false)
  })
})

describe('moveWidget', () => {
  const layout: HomeLayout = {
    main: [
      { id: 'a', visible: true },
      { id: 'b', visible: false },
      { id: 'c', visible: true },
    ],
    side: [{ id: 'x', visible: true }],
  }

  it('reorders within a column', () => {
    // Act
    const result = moveWidget(layout, 'a', 'main', 2)

    // Assert
    expect(ids(result)).toEqual({ main: ['b', 'c', 'a'], side: ['x'] })
  })

  it('moves into the other column, keeping visibility', () => {
    // Act
    const result = moveWidget(layout, 'b', 'side', 0)

    // Assert
    expect(ids(result)).toEqual({ main: ['a', 'c'], side: ['b', 'x'] })
    expect(result.side[0]).toEqual({ id: 'b', visible: false })
  })

  it('leaves the layout alone for an id it does not hold', () => {
    // Act
    const result = moveWidget(layout, 'missing', 'side', 0)

    // Assert
    expect(result).toBe(layout)
  })
})

describe('setWidgetVisible', () => {
  it('changes only the named widget', () => {
    // Arrange
    const layout: HomeLayout = {
      main: [{ id: 'a', visible: true }],
      side: [{ id: 'x', visible: true }],
    }

    // Act
    const result = setWidgetVisible(layout, 'x', false)

    // Assert
    expect(result).toEqual({
      main: [{ id: 'a', visible: true }],
      side: [{ id: 'x', visible: false }],
    })
  })
})

describe('saved widget mapping', () => {
  it('maps the API column codes both ways', () => {
    // Arrange
    const response = [
      { widgetId: 'a', column: HomeLayoutColumn.Main, visible: true },
      { widgetId: 'x', column: HomeLayoutColumn.Side, visible: false },
    ]

    // Act
    const entries = fromSavedWidgets(response)

    // Assert
    expect(entries).toEqual([saved('a', 'main'), saved('x', 'side', false)])
    expect(toSavedWidgets(entries)).toEqual(response)
  })
})
