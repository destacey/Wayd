import type { Mock } from 'vitest'
import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { useState } from 'react'
import CustomizeHomeDrawer from './customize-home-drawer'
import { HomeLayout } from './home-layout'
import { HomeWidget } from './widget-registry'
import { rowsSetting } from './widget-settings'

// antd's Drawer reaches for Next's server-side AsyncLocalStorage, which jsdom
// has no runtime for. Only the Drawer is stubbed, so the rows under test stay real.
vi.mock(import('antd'), async (importOriginal) => {
  const actual = await importOriginal()
  const MockDrawer = ({
    title,
    open,
    onClose,
    footer,
    keyboard,
    children,
  }: any) =>
    open ? (
      <div data-testid="drawer" data-keyboard={String(keyboard)}>
        <div>{title}</div>
        <button onClick={onClose}>Close drawer</button>
        {children}
        {footer}
      </div>
    ) : null
  MockDrawer.displayName = 'MockDrawer'
  // Stubs the component only; nothing here renders antd's static inner panel.
  return { ...actual, Drawer: MockDrawer as unknown as typeof actual.Drawer }
})

// jsdom has no layout, so no sensor can drive a drag. The real DndContext
// renders; its handlers are captured so a test can fire the drag events.
const mockDndRendered = vi.fn()
vi.mock(import('@dnd-kit/core'), async (importOriginal) => {
  const actual = await importOriginal()
  const CapturingDndContext = (props: any) => {
    mockDndRendered(props)
    return <actual.DndContext {...props} />
  }
  // A plain function stands in for the memoized component; nothing reads its memo fields.
  return {
    ...actual,
    DndContext: CapturingDndContext as unknown as typeof actual.DndContext,
  }
})

const drag = async (
  handler: 'onDragStart' | 'onDragOver' | 'onDragEnd' | 'onDragCancel',
  activeId: string,
  overId?: string,
) => {
  await act(async () => {
    // The latest render's handlers, which close over the current layout.
    mockDndRendered.mock.lastCall![0][handler]({
      active: { id: activeId },
      over: overId ? { id: overId } : null,
    })
  })
}

const Nothing = () => null

const widget = (
  id: string,
  title: string,
  overrides: Partial<HomeWidget> = {},
): HomeWidget => ({
  id,
  title,
  description: `${title} description.`,
  defaultColumn: 'main',
  defaultOrder: 10,
  requires: {},
  Component: Nothing,
  ...overrides,
})

const widgets = [
  widget('a', 'Alpha'),
  widget('b', 'Bravo'),
  widget('c', 'Charlie'),
  widget('x', 'X-ray', { settings: [rowsSetting(5)] }),
  widget('hidden-from-account', 'Not offered'),
]

const initialLayout: HomeLayout = {
  main: [
    { id: 'a', visible: true },
    { id: 'b', visible: true },
    { id: 'c', visible: true },
  ],
  side: [{ id: 'x', visible: true }],
}

/** Holds the layout as the page would, so a change shows in the next render. */
const Harness = ({
  onChange,
  onReset = vi.fn(),
  onClose = vi.fn(),
}: {
  onChange: Mock
  onReset?: Mock
  onClose?: Mock
}) => {
  const [layout, setLayout] = useState(initialLayout)
  return (
    <CustomizeHomeDrawer
      open
      layout={layout}
      widgets={widgets}
      mutedTiles={[]}
      onChange={(next) => {
        onChange(next)
        setLayout(next)
      }}
      onMutedTilesChange={vi.fn()}
      onReset={onReset}
      onClose={onClose}
    />
  )
}

/** Widget titles in a column, top to bottom, read from their switches. */
const columnTitles = (column: 'Main' | 'Side') =>
  within(screen.getByRole('region', { name: column }))
    .queryAllByRole('switch')
    .map((s) => s.getAttribute('aria-label')!.replace(/^Show /, ''))

const chooseMove = async (title: string, action: RegExp) => {
  fireEvent.click(screen.getByRole('button', { name: `Move ${title}` }))
  const item = await screen.findByRole('menuitem', { name: action })
  await act(async () => {
    fireEvent.click(item)
  })
}

describe('CustomizeHomeDrawer', () => {
  it('lists each widget of the layout under its column, and nothing else', () => {
    // Act
    render(<Harness onChange={vi.fn()} />)

    // Assert
    expect(columnTitles('Main')).toEqual(['Alpha', 'Bravo', 'Charlie'])
    expect(columnTitles('Side')).toEqual(['X-ray'])
    expect(screen.queryByText('Not offered')).not.toBeInTheDocument()
  })

  it('reorders a widget within its column', async () => {
    // Arrange
    const onChange = vi.fn()
    render(<Harness onChange={onChange} />)

    // Act
    await chooseMove('Alpha', /Move down/)

    // Assert
    expect(columnTitles('Main')).toEqual(['Bravo', 'Alpha', 'Charlie'])
    expect(onChange).toHaveBeenLastCalledWith({
      main: [
        { id: 'b', visible: true },
        { id: 'a', visible: true },
        { id: 'c', visible: true },
      ],
      side: [{ id: 'x', visible: true }],
    })
  })

  it('cannot move the first widget up or the last one down', async () => {
    // Arrange
    render(<Harness onChange={vi.fn()} />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Move Alpha' }))
    const up = await screen.findByRole('menuitem', { name: /Move up/ })

    // Assert
    expect(up).toHaveAttribute('aria-disabled', 'true')
  })

  it('moves a widget to the end of the other column', async () => {
    // Arrange
    const onChange = vi.fn()
    render(<Harness onChange={onChange} />)

    // Act
    await chooseMove('Bravo', /Move to side column/)

    // Assert
    expect(columnTitles('Main')).toEqual(['Alpha', 'Charlie'])
    expect(columnTitles('Side')).toEqual(['X-ray', 'Bravo'])
  })

  it('offers an empty column as a place to drop into', async () => {
    // Arrange
    render(<Harness onChange={vi.fn()} />)

    // Act
    await chooseMove('X-ray', /Move to main column/)

    // Assert
    expect(columnTitles('Side')).toEqual([])
    expect(
      within(screen.getByRole('region', { name: 'Side' })).getByText(
        /Drag a widget here/,
      ),
    ).toBeInTheDocument()
  })

  it('switches a widget off, keeping its place', () => {
    // Arrange
    const onChange = vi.fn()
    render(<Harness onChange={onChange} />)

    // Act
    fireEvent.click(screen.getByRole('switch', { name: 'Show Charlie' }))

    // Assert
    expect(
      screen.getByRole('switch', { name: 'Show Charlie' }),
    ).not.toBeChecked()
    expect(onChange).toHaveBeenLastCalledWith({
      main: [
        { id: 'a', visible: true },
        { id: 'b', visible: true },
        { id: 'c', visible: false },
      ],
      side: [{ id: 'x', visible: true }],
    })
  })

  it('offers settings only on a widget that declares them', () => {
    // Act
    render(<Harness onChange={vi.fn()} />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'X-ray settings' }),
    ).toHaveAttribute('aria-expanded', 'false')
    expect(
      screen.queryByRole('button', { name: 'Alpha settings' }),
    ).not.toBeInTheDocument()
  })

  it('changes a widget’s settings from its opened row, and marks it customized', () => {
    // Arrange
    const onChange = vi.fn()
    render(<Harness onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: 'X-ray settings' }))

    // Act
    fireEvent.click(screen.getByRole('radio', { name: '10' }))

    // Assert
    expect(onChange).toHaveBeenLastCalledWith({
      ...initialLayout,
      side: [{ id: 'x', visible: true, settings: { rows: '10' } }],
    })
    expect(screen.getByText('Customized')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: '10' })).toBeChecked()
  })

  it('resets through Reset to default', () => {
    // Arrange
    const onChange = vi.fn()
    const onReset = vi.fn()
    render(<Harness onChange={onChange} onReset={onReset} />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Reset to default' }))

    // Assert
    expect(onReset).toHaveBeenCalledTimes(1)
    expect(onChange).not.toHaveBeenCalled()
  })

  it('closes through Done', () => {
    // Arrange
    const onClose = vi.fn()
    render(<Harness onChange={vi.fn()} onClose={onClose} />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Done' }))

    // Assert
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('reorders within a column when dropped on another row', async () => {
    // Arrange
    render(<Harness onChange={vi.fn()} />)

    // Act
    await drag('onDragStart', 'a')
    await drag('onDragEnd', 'a', 'c')

    // Assert
    expect(columnTitles('Main')).toEqual(['Bravo', 'Charlie', 'Alpha'])
  })

  it('moves into the other column as the drag crosses it', async () => {
    // Arrange
    render(<Harness onChange={vi.fn()} />)

    // Act
    await drag('onDragStart', 'b')
    await drag('onDragOver', 'b', 'x')
    await drag('onDragEnd', 'b', 'b')

    // Assert
    expect(columnTitles('Main')).toEqual(['Alpha', 'Charlie'])
    expect(columnTitles('Side')).toEqual(['Bravo', 'X-ray'])
  })

  it('moves into the other column when the drop lands there unannounced', async () => {
    // Arrange
    render(<Harness onChange={vi.fn()} />)

    // Act
    await drag('onDragStart', 'b')
    await drag('onDragEnd', 'b', 'column:side')

    // Assert
    expect(columnTitles('Side')).toEqual(['X-ray', 'Bravo'])
  })

  it('leaves a widget in place when dropped in its own column between rows', async () => {
    // Arrange
    const onChange = vi.fn()
    render(<Harness onChange={onChange} />)

    // Act
    await drag('onDragStart', 'a')
    await drag('onDragEnd', 'a', 'column:main')

    // Assert
    expect(columnTitles('Main')).toEqual(['Alpha', 'Bravo', 'Charlie'])
    expect(onChange).not.toHaveBeenCalled()
  })

  it('undoes a move into the other column when the drag is cancelled', async () => {
    // Arrange
    render(<Harness onChange={vi.fn()} />)
    await drag('onDragStart', 'b')
    await drag('onDragOver', 'b', 'x')

    // Act
    await drag('onDragCancel', 'b')

    // Assert
    expect(columnTitles('Main')).toEqual(['Alpha', 'Bravo', 'Charlie'])
    expect(columnTitles('Side')).toEqual(['X-ray'])
  })

  it('keeps Escape from closing the drawer while a drag is in progress', async () => {
    // Arrange
    render(<Harness onChange={vi.fn()} />)
    expect(screen.getByTestId('drawer')).toHaveAttribute(
      'data-keyboard',
      'true',
    )

    // Act
    await drag('onDragStart', 'a')

    // Assert
    expect(screen.getByTestId('drawer')).toHaveAttribute(
      'data-keyboard',
      'false',
    )

    // Act
    await drag('onDragCancel', 'a')

    // Assert
    expect(screen.getByTestId('drawer')).toHaveAttribute(
      'data-keyboard',
      'true',
    )
  })

  it('gives every drag handle a name and keyboard focus', () => {
    // Act
    render(<Harness onChange={vi.fn()} />)

    // Assert
    const handle = screen.getByRole('button', { name: 'Drag Alpha' })
    expect(handle).toHaveAttribute('tabindex', '0')
    expect(handle).toHaveAttribute('aria-roledescription', 'sortable')
  })
})

describe('CustomizeHomeDrawer counts section', () => {
  const tile = (id: string, title: string, priority: number) => ({
    id,
    title,
    priority,
    Tile: Nothing,
  })

  const tiledWidgets = [
    widget('tasks', 'Tasks', {
      tiles: [
        tile('overdue', 'Overdue', 100),
        tile('due-soon', 'Due soon', 60),
      ],
    }),
    widget('news', 'News', { tiles: [tile('unread', 'Unread', 40)] }),
    widget('risks', 'Risks', { tiles: [tile('follow-ups', 'Follow-ups', 80)] }),
    widget('plain', 'Plain'),
  ]

  const tiledLayout: HomeLayout = {
    main: [
      { id: 'news', visible: true },
      { id: 'tasks', visible: true },
    ],
    side: [
      { id: 'risks', visible: false },
      { id: 'plain', visible: true },
    ],
  }

  const TileHarness = ({
    onMutedTilesChange,
    mutedTiles: initial = [],
  }: {
    onMutedTilesChange: Mock
    mutedTiles?: string[]
  }) => {
    const [mutedTiles, setMutedTiles] = useState(initial)
    return (
      <CustomizeHomeDrawer
        open
        layout={tiledLayout}
        widgets={tiledWidgets}
        mutedTiles={mutedTiles}
        onChange={vi.fn()}
        onMutedTilesChange={(next) => {
          onMutedTilesChange(next)
          setMutedTiles(next)
        }}
        onReset={vi.fn()}
        onClose={vi.fn()}
      />
    )
  }

  const section = () =>
    within(screen.getByRole('region', { name: 'Choose counts to show' }))

  it('lists the tiles of the layout’s widgets in the order they take slots', () => {
    // Act
    render(<TileHarness onMutedTilesChange={vi.fn()} />)

    // Assert
    expect(
      section()
        .getAllByRole('switch')
        .map((s) => s.getAttribute('aria-label')),
    ).toEqual([
      'Show the Overdue tile',
      'Show the Follow-ups tile',
      'Show the Due soon tile',
      'Show the Unread tile',
    ])
  })

  it('mutes a tile when its switch is turned off, and unmutes it when turned on', () => {
    // Arrange
    const onMutedTilesChange = vi.fn()
    render(<TileHarness onMutedTilesChange={onMutedTilesChange} />)
    const unread = section().getByRole('switch', {
      name: 'Show the Unread tile',
    })

    // Act
    fireEvent.click(unread)

    // Assert
    expect(onMutedTilesChange).toHaveBeenLastCalledWith(['unread'])
    expect(unread).not.toBeChecked()

    // Act
    fireEvent.click(unread)

    // Assert
    expect(onMutedTilesChange).toHaveBeenLastCalledWith([])
    expect(unread).toBeChecked()
  })

  it('shows a switched-off widget’s tile as unavailable, keeping whether it is muted', () => {
    // Act
    render(
      <TileHarness onMutedTilesChange={vi.fn()} mutedTiles={['follow-ups']} />,
    )

    // Assert
    const followUps = section().getByRole('switch', {
      name: 'Show the Follow-ups tile',
    })
    expect(followUps).toBeDisabled()
    expect(followUps).not.toBeChecked()
    expect(
      section().getByText('Unavailable while Risks is switched off.'),
    ).toBeInTheDocument()
  })

  it('is left out when no widget in the layout offers a tile', () => {
    // Act
    render(<Harness onChange={vi.fn()} />)

    // Assert
    expect(
      screen.queryByRole('region', { name: 'Choose counts to show' }),
    ).not.toBeInTheDocument()
  })

  it('describes each switch by its note', () => {
    // Act
    render(<TileHarness onMutedTilesChange={vi.fn()} />)

    // Assert
    expect(
      section().getByRole('switch', { name: 'Show the Follow-ups tile' }),
    ).toHaveAccessibleDescription('Unavailable while Risks is switched off.')
    expect(
      section().getByRole('switch', { name: 'Show the Unread tile' }),
    ).toHaveAccessibleDescription('From News.')
  })
})
