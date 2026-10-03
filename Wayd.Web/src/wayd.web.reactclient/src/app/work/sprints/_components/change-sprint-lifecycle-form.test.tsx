import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import ChangeSprintLifecycleForm, {
  SprintLifecycleAction,
} from './change-sprint-lifecycle-form'

const startSprint = jest.fn()
const completeSprint = jest.fn()
const reopenSprint = jest.fn()
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
  useStartSprintMutation: () => [startSprint],
  useCompleteSprintMutation: () => [completeSprint],
  useReopenSprintMutation: () => [reopenSprint],
}))

const sprint: SprintDetailsDto = {
  id: 'sprint-2',
  key: 202,
  name: '26.3.3',
  state: { id: 1, name: 'Future' },
  start: '2026-08-31',
  end: '2026-09-13',
  team: { id: 't1', key: 14, name: 'Core Services', code: 'CS', type: 'Team' },
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  canManageSprint: true,
  canStart: true,
  canComplete: false,
  canReopen: false,
}

const openSprint = { id: 'sprint-1', key: 201, name: '26.3.2' }

const renderForm = (
  action: SprintLifecycleAction,
  overrides: Partial<SprintDetailsDto> = {},
  onFormComplete = jest.fn(),
) =>
  render(
    <ChangeSprintLifecycleForm
      sprint={{ ...sprint, ...overrides }}
      action={action}
      onFormComplete={onFormComplete}
      onFormCancel={jest.fn()}
    />,
  )

describe('ChangeSprintLifecycleForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    startSprint.mockResolvedValue({ data: undefined })
    completeSprint.mockResolvedValue({ data: undefined })
    reopenSprint.mockResolvedValue({ data: undefined })
  })

  it('starts without completing another sprint when none is open', async () => {
    // Arrange
    const onFormComplete = jest.fn()
    renderForm(SprintLifecycleAction.Start, {}, onFormComplete)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Start Sprint' }))

    // Assert
    expect(startSprint).toHaveBeenCalledWith({
      id: 'sprint-2',
      key: 202,
      completeOpenSprint: false,
      openSprint: undefined,
    })
    expect(onFormComplete).toHaveBeenCalled()
    expect(screen.queryByText(/also completes/)).not.toBeInTheDocument()
  })

  it('names the open sprint and confirms completing it', async () => {
    // Arrange — confirming this dialog is the confirmation the API asks for.
    renderForm(SprintLifecycleAction.Start, { openSprint })

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Start Sprint' }))

    // Assert
    expect(
      screen.getByText(
        'Starting this sprint also completes "26.3.2" at the same moment.',
      ),
    ).toBeInTheDocument()
    expect(startSprint).toHaveBeenCalledWith({
      id: 'sprint-2',
      key: 202,
      completeOpenSprint: true,
      openSprint,
    })
  })

  it('completes the sprint', async () => {
    // Arrange
    renderForm(SprintLifecycleAction.Complete, { canComplete: true })

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Complete Sprint' }),
    )

    // Assert
    expect(completeSprint).toHaveBeenCalledWith({ id: 'sprint-2', key: 202 })
    expect(successMessage).toHaveBeenCalledWith(
      'Successfully completed sprint.',
    )
  })

  it('does not mention the open sprint when completing', () => {
    // Arrange / Act — only starting closes the other open sprint.
    renderForm(SprintLifecycleAction.Complete, { openSprint })

    // Assert
    expect(screen.queryByText(/also completes/)).not.toBeInTheDocument()
  })

  it('reopens the sprint', async () => {
    // Arrange
    renderForm(SprintLifecycleAction.Reopen, { canReopen: true })

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Reopen Sprint' }))

    // Assert
    expect(reopenSprint).toHaveBeenCalledWith({ id: 'sprint-2', key: 202 })
  })

  it('shows the refusal the API gives and stays open', async () => {
    // Arrange
    const onFormComplete = jest.fn()
    completeSprint.mockResolvedValue({
      error: { status: 400, detail: 'The sprint has not started.' },
    })
    renderForm(SprintLifecycleAction.Complete, {}, onFormComplete)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Complete Sprint' }),
    )

    // Assert
    expect(errorMessage).toHaveBeenCalledWith('The sprint has not started.')
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
