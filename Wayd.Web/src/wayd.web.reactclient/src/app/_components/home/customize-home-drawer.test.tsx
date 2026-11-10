import type { Mock } from 'vitest'
import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { useState } from 'react'
import CustomizeHomeDrawer from './customize-home-drawer'
import { HomeLayout } from './home-layout'
import { HomeWidget } from './widget-registry'

// antd's Drawer reaches for Next's server-side AsyncLocalStorage, which jsdom
// has no runtime for. Only the Drawer is stubbed, so the rows under test stay real.
vi.mock('antd', async () => {
  const actual = await vi.importActual<any>('antd')
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
  return { ...actual, Drawer: MockDrawer }
})

// jsdom has no layout, so no sensor can drive a drag. The real DndContext
// renders; its handlers are captured so a test can fire the drag events.
const mockDndRendered = vi.fn()
vi.mock('@dnd-kit/core', async () => {
  const actual = await vi.importActual<any>('@dnd-kit/core')
  const CapturingDndContext = (props: any) => {
    mockDndRendered(props)
    return <actual.DndContext {...props} />
  }
  return { ...actual, DndContext: CapturingDndContext }
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

const widget = (id: string, title: string): HomeWidget => ({
  id,
  title,
  description: `${title} description.`,
  defaultColumn: 'main',
  defaultOrder: 10,
  requires: {},
  Component: Nothing,
})

const widgets = [
  widget('a', 'Alpha'),
  widget('b', 'Bravo'),
  widget('c', 'Charlie'),
  widget('x', 'X-ray'),
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
      onChange={(next) => {
        onChange(next)
        setLayout(next)
      }}
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
