// The picker compares and formats real moments; the global mock only stubs format.
jest.unmock('dayjs')

import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  SprintDetailsDto,
  SprintType,
  SprintTypeSource,
} from '@/src/services/wayd-api'
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

/** A window from `fromHours` to `toHours` relative to now. */
function window(fromHours: number, toHours?: number) {
  const now = Date.now()
  return {
    earliest: new Date(now + fromHours * 3_600_000),
    latest:
      toHours === undefined ? undefined : new Date(now + toHours * 3_600_000),
  }
}

const sprint: SprintDetailsDto = {
  id: 'sprint-2',
  key: 202,
  name: '26.3.3',
  state: { id: 1, name: 'Future' },
  start: '2026-08-31',
  end: '2026-09-13',
  team: { id: 't1', key: 14, name: 'Team Atlas', code: 'AT', type: 'Team' },
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  teamDaysOff: [],
  canManageSprint: true,
  canStart: true,
  canComplete: false,
  canReopen: false,
  sprintType: SprintType.Standard,
  sprintTypeSource: SprintTypeSource.Default,
  startWindow: window(-48),
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
      completeOpenSprintId: undefined,
      startedAt: expect.any(Date),
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
      completeOpenSprintId: 'sprint-1',
      startedAt: expect.any(Date),
      openSprint,
    })
  })

  it('completes the sprint', async () => {
    // Arrange
    renderForm(SprintLifecycleAction.Complete, {
      canComplete: true,
      completeWindow: window(-24),
    })

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Complete Sprint' }),
    )

    // Assert
    expect(completeSprint).toHaveBeenCalledWith({
      id: 'sprint-2',
      key: 202,
      completedAt: expect.any(Date),
    })
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
    renderForm(
      SprintLifecycleAction.Complete,
      { completeWindow: window(-24) },
      onFormComplete,
    )

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Complete Sprint' }),
    )

    // Assert
    expect(errorMessage).toHaveBeenCalledWith('The sprint has not started.')
    expect(onFormComplete).not.toHaveBeenCalled()
  })

  it('defaults to the latest moment when the window closed before now', async () => {
    // Arrange — the planned end has passed, so only an earlier start can be recorded.
    const closed = window(-72, -24) as { earliest: Date; latest: Date }
    renderForm(SprintLifecycleAction.Start, { startWindow: closed })

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Start Sprint' }))

    // Assert
    const { startedAt } = startSprint.mock.calls[0][0] as { startedAt: Date }
    expect(startedAt.getTime()).toBeLessThanOrEqual(closed.latest.getTime())
    expect(startedAt.getTime()).toBeGreaterThan(
      closed.latest.getTime() - 60_000,
    )
  })

  it('notes when completing a sprint the team did not start', () => {
    // Arrange / Act
    renderForm(SprintLifecycleAction.Complete, {
      canComplete: true,
      completeWindow: window(-24),
    })

    // Assert
    expect(screen.getByText("This sprint wasn't started.")).toBeInTheDocument()
    expect(screen.getByText(/use Correct Actual Dates/)).toBeInTheDocument()
  })

  it('says nothing about the start when completing a started sprint', () => {
    // Arrange / Act
    renderForm(SprintLifecycleAction.Complete, {
      canComplete: true,
      completeWindow: window(-24),
      started: new Date(Date.now() - 48 * 3_600_000),
    })

    // Assert
    expect(
      screen.queryByText("This sprint wasn't started."),
    ).not.toBeInTheDocument()
  })

  it('offers no moment when reopening', () => {
    // Arrange / Act
    renderForm(SprintLifecycleAction.Reopen, { canReopen: true })

    // Assert
    expect(screen.queryByLabelText('Started')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Completed')).not.toBeInTheDocument()
  })
})
