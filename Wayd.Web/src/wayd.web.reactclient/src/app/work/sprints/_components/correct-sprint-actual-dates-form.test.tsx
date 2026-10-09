// The picker compares and formats real moments; the global mock only stubs format.
jest.unmock('dayjs')

import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { SprintDetailsDto, SprintListDto } from '@/src/services/wayd-api'
import CorrectSprintActualDatesForm from './correct-sprint-actual-dates-form'

const correctActualDates = jest.fn()
const errorMessage = jest.fn()
const successMessage = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: errorMessage, success: successMessage }),
}))

// Not spread from the real barrel: it re-exports store-bound hooks. The stand-in
// keeps the hook's contract — OK submits, and a successful submit completes.
jest.mock('@/src/hooks', () => ({
  useConfirmModal: (options: {
    onSubmit: () => Promise<boolean>
    onComplete: () => void
    onCancel: () => void
  }) => ({
    isOpen: true,
    isSaving: false,
    handleOk: async () => {
      if (await options.onSubmit()) options.onComplete()
    },
    handleCancel: options.onCancel,
  }),
}))

jest.mock('@/src/store/features/work-management/sprints-api', () => ({
  useCorrectSprintActualDatesMutation: () => [correctActualDates],
}))

const team = {
  id: 't1',
  key: 14,
  name: 'Team Atlas',
  code: 'CS',
  type: 'Team',
}

const sprintStarted = new Date('2026-08-31T14:00:00Z')
const sprintCompleted = new Date('2026-09-11T20:00:00Z')

const sprint: SprintDetailsDto = {
  id: 'sprint-2',
  key: 202,
  name: '26.3.3',
  state: { id: 1, name: 'Completed' },
  start: '2026-08-31',
  end: '2026-09-13',
  team,
  started: sprintStarted,
  completed: sprintCompleted,
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  teamDaysOff: [],
  canManageSprint: true,
  canStart: false,
  canComplete: false,
  canReopen: false,
}

const nextSprint: SprintListDto = {
  id: 'sprint-3',
  key: 203,
  name: '26.3.4',
  state: { id: 1, name: 'Completed' },
  start: '2026-09-14',
  end: '2026-09-27',
  team,
  started: new Date('2026-09-11T20:00:00Z'),
  completed: new Date('2026-09-25T20:00:00Z'),
}

const renderForm = (
  overrides: Partial<SprintDetailsDto> = {},
  onFormComplete = jest.fn(),
) =>
  render(
    <CorrectSprintActualDatesForm
      sprint={{ ...sprint, ...overrides }}
      nextSprint={nextSprint}
      onFormComplete={onFormComplete}
      onFormCancel={jest.fn()}
    />,
  )

const saveButton = () => screen.getByRole('button', { name: 'Save' })

/**
 * Clears a picker through its clear icon, as a user does. The icon takes
 * pointer events only while the picker is hovered, which jsdom's styles never
 * reflect, so the check is skipped.
 */
const clearPicker = async (label: string) => {
  const picker = screen.getByLabelText(label).closest('.ant-picker')
  const clear = picker?.querySelector('.ant-picker-clear')
  if (!clear) throw new Error(`No clear icon for ${label}`)
  await userEvent.setup({ pointerEventsCheck: 0 }).click(clear)
}

describe('CorrectSprintActualDatesForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    correctActualDates.mockResolvedValue({ data: undefined })
  })

  it('offers no save until a date changes', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(saveButton()).toBeDisabled()
  })

  it('names the neighbouring sprint it can correct together', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByText('203 - 26.3.4')).toBeInTheDocument()
    expect(screen.getByLabelText('26.3.4 started')).toBeInTheDocument()
  })

  it('sends only the sprints that changed, a cleared value following the planned date', async () => {
    // Arrange
    const onFormComplete = jest.fn()
    renderForm({}, onFormComplete)

    // Act
    await clearPicker('Completed')
    await userEvent.click(saveButton())

    // Assert
    expect(correctActualDates).toHaveBeenCalledWith({
      sprints: [
        {
          id: 'sprint-2',
          key: 202,
          started: sprintStarted,
          completed: undefined,
        },
      ],
    })
    expect(successMessage).toHaveBeenCalledWith(
      'Successfully corrected the actual dates.',
    )
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('sends a changed neighbour alongside the sprint', async () => {
    // Arrange
    renderForm()

    // Act
    await clearPicker('Completed')
    await clearPicker('26.3.4 started')
    await userEvent.click(saveButton())

    // Assert
    const { sprints } = correctActualDates.mock.calls[0][0] as {
      sprints: { id: string }[]
    }
    expect(sprints.map((s) => s.id)).toEqual(['sprint-2', 'sprint-3'])
  })

  it('refuses a completion that is not after the start', () => {
    // Arrange / Act
    renderForm({ completed: new Date('2026-08-31T13:00:00Z') })

    // Assert
    expect(screen.getByText('Must be after the start.')).toBeInTheDocument()
    expect(saveButton()).toBeDisabled()
  })

  it('refuses a moment in the future', () => {
    // Arrange / Act
    renderForm({
      started: new Date(Date.now() - 3_600_000),
      completed: new Date(Date.now() + 3_600_000),
    })

    // Assert
    expect(screen.getByText("Can't be in the future.")).toBeInTheDocument()
    expect(saveButton()).toBeDisabled()
  })

  it('shows an unrecorded date as not recorded', async () => {
    // Arrange
    renderForm()

    // Act
    await clearPicker('Completed')

    // Assert
    expect(screen.getByLabelText('Completed')).toHaveAttribute(
      'placeholder',
      'Not recorded',
    )
  })

  it('shows the refusal the API gives and stays open', async () => {
    // Arrange
    const onFormComplete = jest.fn()
    correctActualDates.mockResolvedValue({
      error: {
        status: 400,
        detail:
          "26.3.3 can't be completed after 26.3.4 starts. Correct both together.",
      },
    })
    renderForm({}, onFormComplete)

    // Act
    await clearPicker('Started')
    await userEvent.click(saveButton())

    // Assert
    expect(errorMessage).toHaveBeenCalledWith(
      "26.3.3 can't be completed after 26.3.4 starts. Correct both together.",
    )
    expect(onFormComplete).not.toHaveBeenCalled()
    expect(within(document.body).getByText('203 - 26.3.4')).toBeInTheDocument()
  })
})
