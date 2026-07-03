import { render, screen } from '@testing-library/react'
import { GridSortableRow, useGridDragHandle } from './grid-dnd'

// Mock @dnd-kit/sortable
jest.mock('@dnd-kit/sortable', () => ({
  useSortable: jest.fn(() => ({
    attributes: { role: 'button', tabIndex: 0 },
    listeners: { onMouseDown: jest.fn() },
    setNodeRef: jest.fn(),
    transform: null,
    transition: undefined,
    isDragging: false,
  })),
}))

// Mock @dnd-kit/utilities
jest.mock('@dnd-kit/utilities', () => ({
  CSS: {
    Transform: {
      toString: jest.fn(() => undefined),
    },
  },
}))

describe('GridSortableRow', () => {
  it('renders children in a table row', () => {
    render(
      <table>
        <tbody>
          <GridSortableRow nodeId="test-1" isDragEnabled={true}>
            <td>Cell Content</td>
          </GridSortableRow>
        </tbody>
      </table>,
    )

    expect(screen.getByText('Cell Content')).toBeInTheDocument()
  })

  it('sets data-row-id attribute', () => {
    render(
      <table>
        <tbody>
          <GridSortableRow nodeId="test-1" isDragEnabled={true}>
            <td>Cell</td>
          </GridSortableRow>
        </tbody>
      </table>,
    )

    const row = screen.getByRole('row')
    expect(row).toHaveAttribute('data-row-id', 'test-1')
  })

  it('applies className', () => {
    render(
      <table>
        <tbody>
          <GridSortableRow
            nodeId="test-1"
            isDragEnabled={true}
            className="custom-class"
          >
            <td>Cell</td>
          </GridSortableRow>
        </tbody>
      </table>,
    )

    const row = screen.getByRole('row')
    expect(row).toHaveClass('custom-class')
  })

  it('reduces opacity when parentIsDragging is true', () => {
    render(
      <table>
        <tbody>
          <GridSortableRow
            nodeId="test-1"
            isDragEnabled={true}
            isDragging={true}
          >
            <td>Cell</td>
          </GridSortableRow>
        </tbody>
      </table>,
    )

    const row = screen.getByRole('row')
    expect(row.style.opacity).toBe('0.4')
  })
})

describe('useGridDragHandle', () => {
  it('throws when used outside GridSortableRow', () => {
    const TestComponent = () => {
      useGridDragHandle()
      return <div>test</div>
    }

    // Suppress React error boundary noise
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<TestComponent />)).toThrow(
      'useGridDragHandle must be used within GridSortableRow',
    )

    consoleSpy.mockRestore()
  })

  it('provides drag handle context within GridSortableRow', () => {
    const DragHandleConsumer = () => {
      // This will throw if context is not provided
      const context = useGridDragHandle()
      return (
        <td
          data-testid="handle"
          data-has-listeners={context.listeners != null ? 'true' : 'false'}
          data-has-attributes={context.attributes != null ? 'true' : 'false'}
        >
          Handle
        </td>
      )
    }

    render(
      <table>
        <tbody>
          <GridSortableRow nodeId="test-1" isDragEnabled={true}>
            <DragHandleConsumer />
          </GridSortableRow>
        </tbody>
      </table>,
    )

    const handle = screen.getByTestId('handle')
    expect(handle).toBeInTheDocument()
    expect(handle).toHaveAttribute('data-has-listeners', 'true')
    expect(handle).toHaveAttribute('data-has-attributes', 'true')
  })
})
