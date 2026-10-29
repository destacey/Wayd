import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import DeleteFreeTagForm from './delete-free-tag-form'

const mockError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: jest.fn() }),
}))

jest.mock('@/src/hooks', () => ({
  useConfirmModal: ({
    onSubmit,
    onComplete,
  }: {
    onSubmit: () => Promise<boolean>
    onComplete: () => void
  }) => ({
    isOpen: true,
    isSaving: false,
    handleOk: async () => {
      if (await onSubmit()) onComplete()
    },
    handleCancel: jest.fn(),
  }),
}))

const mockDelete = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useDeleteFreeIntakeRequestTagMutation: () => [mockDelete],
  }),
)

const tag = {
  id: 't-free',
  name: 'follow up',
  isFree: true,
  isActive: true,
  requestCount: 3,
}

const renderForm = async (onComplete = jest.fn()) =>
  await act(async () =>
    render(
      <DeleteFreeTagForm
        tag={tag}
        onFormComplete={onComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )

describe('DeleteFreeTagForm', () => {
  beforeEach(() => {
    mockDelete.mockReset()
    mockError.mockReset()
    mockDelete.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('says how many requests lose the tag, then deletes it', async () => {
    // Arrange
    const onComplete = jest.fn()
    await renderForm(onComplete)

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Delete' })),
    )

    // Assert
    expect(
      screen.getByText(/It will be removed from 3 request\(s\)/),
    ).toBeInTheDocument()
    expect(mockDelete).toHaveBeenCalledWith('t-free')
    expect(onComplete).toHaveBeenCalled()
  })

  it('shows the API’s reason and stays open when the delete is refused', async () => {
    // Arrange
    mockDelete.mockReturnValue({
      unwrap: () =>
        Promise.reject({ detail: 'The tags changed while saving.' }),
    })
    const onComplete = jest.fn()
    await renderForm(onComplete)

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Delete' })),
    )

    // Assert
    expect(mockError).toHaveBeenCalledWith('The tags changed while saving.')
    expect(onComplete).not.toHaveBeenCalled()
  })
})
