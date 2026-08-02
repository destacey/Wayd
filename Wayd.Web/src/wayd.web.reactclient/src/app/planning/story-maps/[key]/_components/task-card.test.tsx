import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { StoryMapTaskDto } from '@/src/services/wayd-api'
import TaskCard from './task-card'
import { BoardActions } from './board-actions'
import { useBoardSortable } from './use-board-sortable'

/**
 * The card's click opens the task drawer, and it shares its surface with three things that must
 * keep their own clicks: the inline title editor, the persona dots, and the delete button. A drag
 * also ends in a click on the card, so these cover that too.
 */

// The real hook needs a DndContext; the card only cares about isDragging and the ref/class values.
jest.mock('./use-board-sortable', () => ({
  useBoardSortable: jest.fn(),
}))

const mockUseBoardSortable = useBoardSortable as jest.Mock

const buildTask = (overrides: Partial<StoryMapTaskDto> = {}): StoryMapTaskDto =>
  ({
    id: 'task-1',
    stepId: 'step-1',
    swimLaneId: 'lane-1',
    title: 'Write the thing',
    order: 0,
    personaIds: [],
    checklist: [],
    checklistCompletedCount: 0,
    checklistTotalCount: 0,
    ...overrides,
  }) as StoryMapTaskDto

const mockOnSelectTask = jest.fn()

const buildActions = (overrides: Partial<BoardActions> = {}): BoardActions =>
  ({
    canUpdate: true,
    autoEditId: null,
    onAutoEditEnd: jest.fn(),
    personas: [],
    onSelectTask: mockOnSelectTask,
    selectedTaskId: null,
    onRenameTask: jest.fn(),
    onDeleteTask: jest.fn(),
    onToggleTaskPersona: jest.fn(),
    ...overrides,
  }) as unknown as BoardActions

const setSortable = (isDragging = false) => {
  mockUseBoardSortable.mockReturnValue({
    attributes: {},
    listeners: {},
    setNodeRef: jest.fn(),
    style: {},
    isDragging,
    dragClassName: '',
    isDropTarget: false,
    dropsAfter: false,
  })
}

const renderCard = (props: Partial<Parameters<typeof TaskCard>[0]> = {}) =>
  render(
    <TaskCard
      task={buildTask()}
      muted={false}
      actions={buildActions()}
      dropSide="before"
      isSelected={false}
      {...props}
    />,
  )

beforeEach(() => {
  jest.clearAllMocks()
  setSortable()
})

describe('TaskCard', () => {
  it('selects the task when the card body is clicked', async () => {
    // Arrange
    const user = userEvent.setup()
    renderCard()

    // Act
    await user.click(screen.getByText('Write the thing').closest('div')!)

    // Assert
    expect(mockOnSelectTask).toHaveBeenCalledWith('task-1')
  })

  it('deselects when the already-selected card is clicked again', async () => {
    // Arrange — the card is both the way into the panel and the way out.
    const user = userEvent.setup()
    renderCard({ isSelected: true })

    // Act
    await user.click(screen.getByText('Write the thing').closest('div')!)

    // Assert
    expect(mockOnSelectTask).toHaveBeenCalledWith(null)
  })

  it('does not select when the click lands on the inline title editor', async () => {
    // Arrange — the title is a button that opens the rename editor, not a drawer trigger.
    const user = userEvent.setup()
    renderCard()

    // Act
    await user.click(screen.getByRole('button', { name: 'Rename task' }))

    // Assert
    expect(mockOnSelectTask).not.toHaveBeenCalled()
  })

  it('does not select when the click lands on the delete button', async () => {
    // Arrange
    const user = userEvent.setup()
    renderCard()

    // Act
    await user.click(screen.getByRole('button', { name: 'Delete task' }))

    // Assert
    expect(mockOnSelectTask).not.toHaveBeenCalled()
  })

  it('does not select when the click is the tail of a drag', async () => {
    // Arrange — a drag ends in a click event on the card; isDragging is what separates the two.
    const user = userEvent.setup()
    setSortable(true)
    renderCard()

    // Act
    await user.click(screen.getByText('Write the thing').closest('div')!)

    // Assert
    expect(mockOnSelectTask).not.toHaveBeenCalled()
  })

  it('marks the card when it is the selected task', () => {
    // Arrange / Act
    const { container } = renderCard({ isSelected: true })

    // Assert
    expect(container.firstChild).toHaveClass('taskCardSelected')
  })
})
