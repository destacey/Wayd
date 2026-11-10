import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import DeleteOperatingModelForm, {
  findReinstatedModel,
} from './delete-operating-model-form'

// Mock window.getComputedStyle for Ant Design Modal
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

const mockMessageSuccess = vi.fn()
const mockMessageError = vi.fn()
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: mockMessageSuccess,
    error: mockMessageError,
  }),
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    hasPermissionClaim: () => true,
  }),
}))

const current = {
  start: '2025-07-01',
  timeZone: 'America/Chicago',
  isCurrent: true,
}
const previous = {
  start: '2025-01-01',
  timeZone: 'UTC',
  isCurrent: false,
}
const oldest = {
  start: '2024-01-01',
  timeZone: 'Europe/London',
  isCurrent: false,
}

describe('findReinstatedModel', () => {
  it('returns the latest model before the current one', () => {
    expect(findReinstatedModel([oldest, current, previous])).toBe(previous)
  })

  it('returns undefined when only the current model exists', () => {
    expect(findReinstatedModel([current])).toBeUndefined()
  })
})

describe('DeleteOperatingModelForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('names the model that becomes current again', () => {
    render(
      <DeleteOperatingModelForm
        operatingModel={current}
        reinstatedModel={previous}
        deleteOperatingModel={vi.fn()}
        onFormComplete={vi.fn()}
        onFormCancel={vi.fn()}
      />,
    )

    expect(
      screen.getByText(
        'Are you sure you want to delete the current operating model?',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('America/Chicago')).toBeInTheDocument()
    expect(screen.getByText(/The model from 1\/1\/2025/)).toBeInTheDocument()
  })

  it('deletes only once the user confirms', async () => {
    const deleteOperatingModel = vi.fn().mockResolvedValue(undefined)
    const onFormComplete = vi.fn()
    render(
      <DeleteOperatingModelForm
        operatingModel={current}
        reinstatedModel={previous}
        deleteOperatingModel={deleteOperatingModel}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )
    expect(deleteOperatingModel).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(onFormComplete).toHaveBeenCalled())
    expect(deleteOperatingModel).toHaveBeenCalledTimes(1)
    expect(mockMessageSuccess).toHaveBeenCalledWith(
      'Successfully deleted operating model.',
    )
  })

  it('keeps the modal open and shows the API error when the delete fails', async () => {
    const deleteOperatingModel = vi.fn().mockRejectedValue({
      status: 400,
      detail: 'Cannot remove the last operating model.',
    })
    const onFormComplete = vi.fn()
    render(
      <DeleteOperatingModelForm
        operatingModel={current}
        reinstatedModel={undefined}
        deleteOperatingModel={deleteOperatingModel}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    await waitFor(() =>
      expect(mockMessageError).toHaveBeenCalledWith(
        'Cannot remove the last operating model.',
      ),
    )
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
