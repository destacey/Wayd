import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { StoryMapDetailsDto, StoryMapTaskDto } from '@/src/services/wayd-api'
import TaskDrawer from './task-drawer'

/**
 * Description and title are written through separate endpoints so concurrent edits to one cannot
 * revert the other. These assert the drawer actually keeps them apart — sending the title alongside
 * the description is the bug the split exists to prevent.
 */

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// The checklist and work-item controls each own a mutation; none are exercised here.
const noopMutation = () => [
  jest.fn(() => ({ unwrap: () => Promise.resolve() })),
]
jest.mock('@/src/store/features/planning/story-maps-api', () => ({
  useAddChecklistItemMutation: () => noopMutation(),
  useRenameChecklistItemMutation: () => noopMutation(),
  useSetChecklistItemCheckedMutation: () => noopMutation(),
  useRemoveChecklistItemMutation: () => noopMutation(),
  useLinkWorkItemMutation: () => noopMutation(),
  useUnlinkWorkItemMutation: () => noopMutation(),
}))

const buildTask = (overrides: Partial<StoryMapTaskDto> = {}): StoryMapTaskDto =>
  ({
    id: 'task-1',
    stepId: 'step-1',
    swimLaneId: 'lane-1',
    title: 'Write the thing',
    description: undefined,
    order: 0,
    personaIds: [],
    checklist: [],
    checklistCompletedCount: 0,
    checklistTotalCount: 0,
    ...overrides,
  }) as StoryMapTaskDto

const buildMap = (
  personas: { id: string; name: string; color: string; order: number }[] = [],
): StoryMapDetailsDto =>
  ({
    id: 'map-1',
    key: 1,
    name: 'Map',
    status: 'Active',
    goals: [],
    personas,
    swimLanes: [{ id: 'lane-1', name: 'Tasks', order: 0, isDefault: true }],
  }) as unknown as StoryMapDetailsDto

const mockOnRenameTask = jest.fn()
const mockOnSetTaskDescription = jest.fn()
const mockOnClose = jest.fn()

const renderDrawer = (
  task: StoryMapTaskDto = buildTask(),
  map: StoryMapDetailsDto = buildMap(),
) =>
  render(
    <TaskDrawer
      map={map}
      storyMapKey="1"
      task={task}
      canUpdate
      onClose={mockOnClose}
      onRenameTask={mockOnRenameTask}
      onSetTaskDescription={mockOnSetTaskDescription}
      onDeleteTask={jest.fn()}
      onToggleTaskPersona={jest.fn()}
      onMoveTaskToLane={jest.fn()}
    />,
  )

beforeEach(() => jest.clearAllMocks())

describe('TaskDrawer', () => {
  it('saves the description without sending the title', async () => {
    // Arrange
    const user = userEvent.setup()
    renderDrawer()

    // Act
    const description = screen.getByPlaceholderText(
      'What still needs deciding?',
    )
    await user.type(description, 'Some description')
    await user.tab()

    // Assert
    expect(mockOnSetTaskDescription).toHaveBeenCalledWith(
      'task-1',
      'Some description',
    )
    expect(mockOnRenameTask).not.toHaveBeenCalled()
  })

  it('clears the description when emptied', async () => {
    // Arrange
    const user = userEvent.setup()
    renderDrawer(buildTask({ description: 'Existing description' }))

    // Act
    await user.clear(screen.getByPlaceholderText('What still needs deciding?'))
    await user.tab()

    // Assert — undefined, not '', so the field is cleared rather than set to an empty string.
    expect(mockOnSetTaskDescription).toHaveBeenCalledWith('task-1', undefined)
  })

  it('sends undefined rather than an empty string when cleared', async () => {
    // Arrange — the description column is nullable; JSON.stringify drops an undefined property, so
    // the API binds Description to null. Passing '' would store an empty string instead.
    const user = userEvent.setup()
    renderDrawer(buildTask({ description: 'Existing description' }))

    // Act
    await user.clear(screen.getByPlaceholderText('What still needs deciding?'))
    await user.tab()

    // Assert
    const [, sent] = mockOnSetTaskDescription.mock.calls[0]
    expect(sent).toBeUndefined()
    expect(sent).not.toBe('')
    expect(JSON.stringify({ description: sent })).toBe('{}')
  })

  it('does not save when the description is only whitespace-different', async () => {
    // Arrange — a stored value can itself be whitespace; an untrimmed compare would read a no-op
    // blur as a change and fire a spurious write.
    const user = userEvent.setup()
    renderDrawer(buildTask({ description: '  ' }))

    // Act — focus and blur without editing.
    await user.click(screen.getByPlaceholderText('What still needs deciding?'))
    await user.tab()

    // Assert
    expect(mockOnSetTaskDescription).not.toHaveBeenCalled()
  })

  it('does not save when the description is unchanged', async () => {
    // Arrange
    const user = userEvent.setup()
    renderDrawer(buildTask({ description: 'Existing description' }))

    // Act — focus and blur without editing.
    await user.click(screen.getByPlaceholderText('What still needs deciding?'))
    await user.tab()

    // Assert
    expect(mockOnSetTaskDescription).not.toHaveBeenCalled()
  })

  it('renames without sending the description', async () => {
    // Arrange
    const user = userEvent.setup()
    renderDrawer(buildTask({ description: 'Existing description' }))

    // Act
    const title = screen.getByLabelText('Task title')
    await user.clear(title)
    await user.type(title, 'Renamed')
    await user.tab()

    // Assert
    expect(mockOnRenameTask).toHaveBeenCalledWith('task-1', 'Renamed')
    expect(mockOnSetTaskDescription).not.toHaveBeenCalled()
  })

  it('colours a linked persona chip with the persona colour, not the theme primary', () => {
    // Arrange — one linked persona, one not, so both treatments render side by side.
    const map = buildMap([
      { id: 'p-1', name: 'Participant', color: '#52C41A', order: 0 },
      { id: 'p-2', name: 'Facilitator', color: '#722ED1', order: 1 },
    ])

    // Act
    renderDrawer(buildTask({ personaIds: ['p-1'] }), map)

    // Assert — the linked chip takes its own colour; the unlinked one is left to the theme tokens.
    const linked = screen.getByRole('button', { name: /Participant/ })
    expect(linked).toHaveStyle({ borderColor: '#52C41A', color: '#52C41A' })
    expect(linked).toHaveAttribute('aria-pressed', 'true')

    const unlinked = screen.getByRole('button', { name: /Facilitator/ })
    expect(unlinked.style.borderColor).toBe('')
    expect(unlinked.style.color).toBe('')
    expect(unlinked).toHaveAttribute('aria-pressed', 'false')
  })

  it('shows a hollow dot on an unlinked persona and a filled one when linked', () => {
    // Arrange
    const map = buildMap([
      { id: 'p-1', name: 'Participant', color: '#52C41A', order: 0 },
      { id: 'p-2', name: 'Facilitator', color: '#722ED1', order: 1 },
    ])

    // Act
    renderDrawer(buildTask({ personaIds: ['p-1'] }), map)

    // Assert — both keep the circle; only the fill differs, as on the task card.
    const dotOf = (name: RegExp) =>
      screen
        .getByRole('button', { name })
        .querySelector<HTMLElement>('.personaDot')

    const linkedDot = dotOf(/Participant/)
    expect(linkedDot).toHaveStyle({ backgroundColor: '#52C41A' })
    expect(linkedDot?.className).not.toContain('personaDotUnlinked')

    const unlinkedDot = dotOf(/Facilitator/)
    expect(unlinkedDot).toBeInTheDocument()
    expect(unlinkedDot?.className).toContain('personaDotUnlinked')
    expect(unlinkedDot?.style.backgroundColor).toBe('')
  })

  it('renders a character counter against each field limit', () => {
    // Arrange / Act — limits match the command validators (128 / 2048).
    renderDrawer(buildTask({ title: 'Write the thing' }))

    // Assert — mounted at all times; CSS :focus-within is what reveals it, which jsdom cannot
    // evaluate, so the reveal itself is left to a browser check.
    expect(screen.getByText('15 / 128')).toBeInTheDocument()
    expect(screen.getByText('0 / 2048')).toBeInTheDocument()
  })

  it('keeps focus in the title while typing', async () => {
    // Arrange — toggling antd's `showCount` on focus remounts the textarea, which drops focus on
    // the first keystroke. This guards the CSS-based approach that avoids the remount.
    const user = userEvent.setup()
    renderDrawer(buildTask({ title: 'Start' }))

    // Act
    const title = screen.getByLabelText('Task title')
    await user.click(title)
    await user.type(title, ' more')

    // Assert — same node still focused, and every character landed.
    expect(document.activeElement).toBe(screen.getByLabelText('Task title'))
    expect(screen.getByLabelText('Task title')).toHaveValue('Start more')
  })

  it('makes a checklist item name click-to-edit with no pencil trigger', async () => {
    // Arrange
    const user = userEvent.setup()
    renderDrawer(
      buildTask({
        checklist: [
          { id: 'i-1', name: 'Draft the copy', isChecked: false, order: 0 },
        ],
        checklistTotalCount: 1,
      }),
    )

    // Assert — no separate edit affordance; the text carries it.
    expect(
      screen.queryByRole('button', { name: /edit/i }),
    ).not.toBeInTheDocument()

    // Act — clicking the name opens the editor.
    await user.click(screen.getByText('Draft the copy'))

    // Assert — editor opens, with no enter-hint glyph: Title and Description are plain textareas
    // with no such affordance, so the drawer keeps one editing convention.
    const editor = screen.getByDisplayValue('Draft the copy')
    expect(editor).toBeInTheDocument()
    expect(
      editor
        .closest('[class*="edit-content"]')
        ?.querySelector('.anticon-enter'),
    ).toBeNull()
  })

  it('disables a checklist row whose id is still a temp placeholder', async () => {
    // Arrange — an optimistic insert holds a temp id until the server responds. Acting on it would
    // send an id the server has never seen, so the row waits.
    const user = userEvent.setup()
    renderDrawer(
      buildTask({
        checklist: [
          { id: 'temp-abc', name: 'Just added', isChecked: false, order: 0 },
          { id: 'i-2', name: 'Already saved', isChecked: false, order: 1 },
        ],
        checklistTotalCount: 2,
      }),
    )

    // Assert — the pending row's controls are inert.
    expect(screen.getByRole('checkbox', { name: 'Just added' })).toBeDisabled()
    const [pendingDelete] = screen.getAllByRole('button', {
      name: 'Delete checklist item',
    })
    expect(pendingDelete).toBeDisabled()

    // Act — clicking the pending name must not open an editor.
    await user.click(screen.getByText('Just added'))

    // Assert
    expect(screen.queryByDisplayValue('Just added')).not.toBeInTheDocument()

    // Assert — a saved row alongside it is unaffected.
    expect(
      screen.getByRole('checkbox', { name: 'Already saved' }),
    ).not.toBeDisabled()
    await user.click(screen.getByText('Already saved'))
    expect(screen.getByDisplayValue('Already saved')).toBeInTheDocument()
  })

  it('renders the checklist delete action as danger', () => {
    // Arrange / Act — matches the Delete task button below it.
    renderDrawer(
      buildTask({
        checklist: [
          { id: 'i-1', name: 'Draft the copy', isChecked: false, order: 0 },
        ],
        checklistTotalCount: 1,
      }),
    )

    // Assert
    expect(
      screen.getByRole('button', { name: 'Delete checklist item' }),
    ).toHaveClass('ant-btn-dangerous')
  })

  /**
   * Escape cancels the editor it is pressed in and must not reach the Drawer, which would close the
   * whole thing mid-edit and discard the draft.
   */
  describe('Escape', () => {
    it('abandons the title draft without closing the drawer', async () => {
      // Arrange
      const user = userEvent.setup()
      renderDrawer()

      // Act
      const title = screen.getByLabelText('Task title')
      await user.clear(title)
      await user.type(title, 'Half typed{Escape}')

      // Assert
      expect(screen.getByLabelText('Task title')).toHaveValue('Write the thing')
      expect(mockOnClose).not.toHaveBeenCalled()
    })

    it('abandons the description draft without closing the drawer', async () => {
      // Arrange
      const user = userEvent.setup()
      renderDrawer(buildTask({ description: 'Existing description' }))

      // Act
      const description = screen.getByLabelText('Task description')
      await user.clear(description)
      await user.type(description, 'Half typed{Escape}')

      // Assert
      expect(screen.getByLabelText('Task description')).toHaveValue(
        'Existing description',
      )
      expect(mockOnSetTaskDescription).not.toHaveBeenCalled()
      expect(mockOnClose).not.toHaveBeenCalled()
    })

    it('cancels adding a checklist item without closing the drawer', async () => {
      // Arrange
      const user = userEvent.setup()
      renderDrawer()

      // Act
      await user.click(screen.getByRole('button', { name: /Add item/ }))
      await user.type(
        screen.getByPlaceholderText('Checklist item'),
        'Half typed{Escape}',
      )

      // Assert
      expect(
        screen.queryByPlaceholderText('Checklist item'),
      ).not.toBeInTheDocument()
      expect(mockOnClose).not.toHaveBeenCalled()
    })

    it('cancels a checklist item rename without closing the drawer', async () => {
      // Arrange
      const user = userEvent.setup()
      renderDrawer(
        buildTask({
          checklist: [
            { id: 'i-1', name: 'Draft the copy', isChecked: false, order: 0 },
          ],
          checklistTotalCount: 1,
        }),
      )

      // Act
      await user.click(screen.getByText('Draft the copy'))
      await user.type(screen.getByDisplayValue('Draft the copy'), '{Escape}')

      // Assert
      expect(screen.getByText('Draft the copy')).toBeInTheDocument()
      expect(mockOnClose).not.toHaveBeenCalled()
    })
  })

  it('reverts an emptied title rather than saving a blank one', async () => {
    // Arrange — title is required, unlike description.
    const user = userEvent.setup()
    renderDrawer()

    // Act
    const title = screen.getByLabelText('Task title')
    await user.clear(title)
    await user.tab()

    // Assert
    expect(mockOnRenameTask).not.toHaveBeenCalled()
    expect(screen.getByLabelText('Task title')).toHaveValue('Write the thing')
  })

  it('abandons the title draft on Escape', async () => {
    // Arrange
    const user = userEvent.setup()
    renderDrawer()

    // Act
    const title = screen.getByLabelText('Task title')
    await user.clear(title)
    await user.type(title, 'Half typed{Escape}')

    // Assert
    expect(mockOnRenameTask).not.toHaveBeenCalled()
    expect(screen.getByLabelText('Task title')).toHaveValue('Write the thing')
  })
})
