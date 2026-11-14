import { HomeLayoutColumn } from '@/src/services/wayd-api'
import {
  arrangeFromLayout,
  fromSavedWidgets,
  HomeLayout,
  layoutToSave,
  moveWidget,
  mutedTilesToSave,
  resolveLayout,
  SavedWidget,
  settingsChangeToSave,
  setTileMuted,
  setWidgetSettings,
  setWidgetVisible,
  tileChoices,
  toSavedWidgets,
} from './home-layout'
import {
  attentionTiles,
  HomeWidget,
  PlaceWidget,
  WidgetAccess,
  WidgetColumn,
} from './widget-registry'
import { rowsSetting } from './widget-settings'

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

const tile = (id: string, priority = 0) => ({
  id,
  title: id,
  priority,
  Tile: Nothing,
})

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
  settings?: Record<string, string>,
): SavedWidget => ({
  widgetId,
  column,
  visible,
  ...(settings ? { settings } : {}),
})

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

  it('lays out the placement it is given when nothing is saved', () => {
    // Arrange: an involvement-style placement that leads with y, moved to main.
    const place: PlaceWidget = (w) =>
      w.id === 'y'
        ? { column: 'main', order: 0 }
        : { column: w.defaultColumn, order: w.defaultOrder }

    // Act
    const layout = resolveLayout(registry, access(), [], place)

    // Assert
    expect(ids(layout)).toEqual({ main: ['y', 'a', 'b', 'c'], side: ['x'] })
  })

  it('puts a widget the layout does not mention where the placement puts it', () => {
    // Arrange: y became relevant after the layout was saved, and now leads main.
    const layout = [
      saved('a', 'main'),
      saved('b', 'main'),
      saved('c', 'main'),
      saved('x', 'side'),
    ]
    const place: PlaceWidget = (w) =>
      w.id === 'y'
        ? { column: 'main', order: 0 }
        : { column: w.defaultColumn, order: w.defaultOrder }

    // Act
    const result = resolveLayout(registry, access(), layout, place)

    // Assert
    expect(ids(result)).toEqual({ main: ['y', 'a', 'b', 'c'], side: ['x'] })
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

    // Assert — all equal in priority, so page order decides; the strip caps them later
    expect(tiles.map((t) => t.id)).toEqual(['y1', 'y2', 'x1', 'b1', 'b2'])
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
      {
        widgetId: 'a',
        column: HomeLayoutColumn.Main,
        visible: true,
        settings: {},
      },
      {
        widgetId: 'x',
        column: HomeLayoutColumn.Side,
        visible: false,
        settings: {},
      },
    ]

    // Act
    const entries = fromSavedWidgets(response)

    // Assert
    expect(entries).toEqual([saved('a', 'main'), saved('x', 'side', false)])
    expect(toSavedWidgets(entries)).toEqual(response)
  })

  it('keeps a replaced widget’s place and visibility for its replacement', () => {
    // Arrange
    const response = [
      {
        widgetId: 'my-team-sprints',
        column: HomeLayoutColumn.Main,
        visible: false,
        settings: {},
      },
    ]

    // Act
    const entries = fromSavedWidgets(response)

    // Assert
    expect(entries).toEqual([saved('my-teams', 'main', false)])
  })

  it('carries a widget’s settings both ways', () => {
    // Arrange
    const response = [
      {
        widgetId: 'a',
        column: HomeLayoutColumn.Main,
        visible: true,
        settings: { rows: '10' },
      },
    ]

    // Act
    const entries = fromSavedWidgets(response)

    // Assert
    expect(entries).toEqual([saved('a', 'main', true, { rows: '10' })])
    expect(toSavedWidgets(entries)).toEqual(response)
  })
})

describe('widget settings in a layout', () => {
  // a and x take a row count; b declares no settings.
  const tunable = [
    widget('a', 'main', 10, { settings: [rowsSetting(5)] }),
    widget('b', 'main', 20),
    widget('x', 'side', 10, { settings: [rowsSetting(5)] }),
  ]

  it('keeps a saved widget’s settings when resolving', () => {
    // Act
    const layout = resolveLayout(tunable, access(), [
      saved('a', 'main', true, { rows: '10' }),
    ])

    // Assert
    expect(layout.main[0]).toEqual({
      id: 'a',
      visible: true,
      settings: { rows: '10' },
    })
  })

  it('sets only the named widget’s settings', () => {
    // Arrange
    const layout = resolveLayout(tunable, access(), [])

    // Act
    const result = setWidgetSettings(layout, 'x', { rows: '3' })

    // Assert
    expect(result.side).toEqual([
      { id: 'x', visible: true, settings: { rows: '3' } },
    ])
    expect(result.main).toEqual(layout.main)
  })

  it('saves only values that differ from the default and that the widget declares', () => {
    // Arrange
    const layout: HomeLayout = {
      main: [
        { id: 'a', visible: true, settings: { rows: '5', retired: 'x' } },
        { id: 'b', visible: true, settings: { rows: '10' } },
      ],
      side: [{ id: 'x', visible: true, settings: { rows: '10' } }],
    }

    // Act
    const result = layoutToSave(layout, [], tunable)

    // Assert
    expect(result).toEqual([
      saved('a', 'main'),
      saved('b', 'main'),
      saved('x', 'side', true, { rows: '10' }),
    ])
  })

  it('keeps the settings of a saved widget the account cannot use', () => {
    // Arrange
    const basis = [saved('a', 'main'), saved('x', 'side', true, { rows: '10' })]
    const layout: HomeLayout = {
      main: [{ id: 'a', visible: true }],
      side: [],
    }

    // Act
    const result = layoutToSave(layout, basis, tunable)

    // Assert
    expect(result).toEqual(basis)
  })

  it('saves the changed widget alone for a user who has arranged nothing', () => {
    // Arrange
    const layout = setWidgetSettings(
      resolveLayout(tunable, access(), []),
      'x',
      { rows: '10' },
    )

    // Act
    const result = settingsChangeToSave(layout, [], tunable, 'x')

    // Assert
    expect(result).toEqual([saved('x', 'side', true, { rows: '10' })])
  })

  it('saves nothing when the only change is put back to its default', () => {
    // Arrange
    const layout = setWidgetSettings(
      resolveLayout(tunable, access(), []),
      'x',
      {},
    )

    // Act
    const result = settingsChangeToSave(layout, [], tunable, 'x')

    // Assert
    expect(result).toEqual([])
  })

  it('adds a changed widget to a saved layout without adding the rest', () => {
    // Arrange
    const basis = [saved('x', 'side', true, { rows: '10' })]
    const layout = setWidgetSettings(
      resolveLayout(tunable, access(), basis),
      'a',
      { rows: '3' },
    )

    // Act
    const result = settingsChangeToSave(layout, basis, tunable, 'a')

    // Assert
    expect(result).toEqual([
      saved('a', 'main', true, { rows: '3' }),
      saved('x', 'side', true, { rows: '10' }),
    ])
  })

  it('keeps every widget of an arranged layout when one’s settings change', () => {
    // Arrange
    const basis = [saved('b', 'main'), saved('a', 'main'), saved('x', 'side')]
    const layout = setWidgetSettings(
      resolveLayout(tunable, access(), basis),
      'a',
      { rows: '3' },
    )

    // Act
    const result = settingsChangeToSave(layout, basis, tunable, 'a')

    // Assert
    expect(result).toEqual([
      saved('b', 'main'),
      saved('a', 'main', true, { rows: '3' }),
      saved('x', 'side'),
    ])
  })
})

describe('muted tiles', () => {
  const widgets = [
    widget('tasks', 'main', 10, {
      tiles: [tile('overdue', 100), tile('due-soon', 60)],
    }),
    widget('news', 'main', 20, { tiles: [tile('unread', 40)] }),
    widget('risks', 'side', 10, { tiles: [tile('follow-ups', 60)] }),
  ]

  it('mutes a tile once, and unmutes it', () => {
    // Arrange
    const muted = ['unread']

    // Act
    const added = setTileMuted(muted, 'overdue', true)
    const again = setTileMuted(added, 'overdue', true)
    const removed = setTileMuted(again, 'unread', false)

    // Assert
    expect(added).toEqual(['unread', 'overdue'])
    expect(again).toEqual(['unread', 'overdue'])
    expect(removed).toEqual(['overdue'])
  })

  it('saves each muted id once, dropping ids the registry no longer has', () => {
    // Act
    const toSave = mutedTilesToSave(
      ['unread', 'retired-tile', 'unread', 'follow-ups'],
      widgets,
    )

    // Assert
    expect(toSave).toEqual(['unread', 'follow-ups'])
  })

  it('offers the layout’s tiles by priority, ties in page order, with whether each widget shows', () => {
    // Arrange
    const layout: HomeLayout = {
      main: [
        { id: 'news', visible: true },
        { id: 'tasks', visible: true },
      ],
      side: [{ id: 'risks', visible: false }],
    }

    // Act
    const choices = tileChoices(layout, widgets)

    // Assert
    expect(
      choices.map((c) => [c.tile.id, c.widgetTitle, c.widgetVisible]),
    ).toEqual([
      ['overdue', 'tasks', true],
      ['due-soon', 'tasks', true],
      ['follow-ups', 'risks', false],
      ['unread', 'news', true],
    ])
  })

  it('offers no tile of a widget missing from the layout', () => {
    // Arrange
    const layout: HomeLayout = {
      main: [{ id: 'news', visible: true }],
      side: [],
    }

    // Act
    const choices = tileChoices(layout, widgets)

    // Assert
    expect(choices.map((c) => c.tile.id)).toEqual(['unread'])
  })
})
