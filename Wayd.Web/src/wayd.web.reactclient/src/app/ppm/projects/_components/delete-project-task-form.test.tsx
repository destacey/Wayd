const mockMessage = {
  success: vi.fn(),
  error: vi.fn(),
  warning: vi.fn(),
  info: vi.fn(),
}
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))
vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))

const mockDelete = vi.fn()
vi.mock('@/src/store/features/ppm/project-tasks-api', () => ({
  useDeleteProjectTaskMutation: () => [mockDelete],
  useGetProjectTaskQuery: () => ({
    data: { id: 't1', key: 'P-1', name: 'Design' },
    isLoading: false,
  }),
}))

import { fireEvent, render, screen, act } from '@testing-library/react'
import DeleteProjectTaskForm from './delete-project-task-form'

describe('DeleteProjectTaskForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockDelete.mockResolvedValue({})
  })

  const renderForm = async (
    onFormComplete = vi.fn(),
    onFormCancel = vi.fn(),
  ) => {
    render(
      <DeleteProjectTaskForm
        projectIdOrKey="P"
        taskIdOrKey="t1"
        onFormComplete={onFormComplete}
        onFormCancel={onFormCancel}
      />,
    )
    await act(async () => {})
    return { onFormComplete, onFormCancel }
  }

  it('deletes the task when Enter is pressed in the dialog', async () => {
    // Arrange
    const { onFormComplete } = await renderForm()

    // Act
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Enter' })
    await act(async () => {})

    // Assert
    expect(mockDelete).toHaveBeenCalledWith({ projectIdOrKey: 'P', id: 't1' })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('leaves Enter on Cancel to the Cancel button', async () => {
    // Arrange
    await renderForm()

    // Act
    fireEvent.keyDown(screen.getByRole('button', { name: 'Cancel' }), {
      key: 'Enter',
    })
    await act(async () => {})

    // Assert
    expect(mockDelete).not.toHaveBeenCalled()
  })

  it('deletes the task when Delete is pressed', async () => {
    // Arrange
    const onFormComplete = vi.fn()
    render(
      <DeleteProjectTaskForm
        projectIdOrKey="P"
        taskIdOrKey="t1"
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )
    await act(async () => {})

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))
    await act(async () => {})

    // Assert
    expect(mockDelete).toHaveBeenCalledWith({ projectIdOrKey: 'P', id: 't1' })
    expect(onFormComplete).toHaveBeenCalled()
  })
})
