import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeTagActiveForm from './change-tag-active-form'

const mockError = vi.fn()
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: vi.fn() }),
}))

vi.mock('@/src/hooks', () => ({
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
    handleCancel: vi.fn(),
  }),
}))

const mockSetActive = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useSetIntakeRequestTagActiveMutation: () => [mockSetActive],
}))

const tag = { id: 't-1', name: 'Payments', isActive: true, requestCount: 4 }

describe('ChangeTagActiveForm', () => {
  beforeEach(() => {
    mockSetActive.mockReset()
    mockError.mockReset()
    mockSetActive.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('deactivates the tag, saying the requests carrying it keep it', async () => {
    // Arrange
    const onComplete = vi.fn()
    await act(async () =>
      render(
        <ChangeTagActiveForm
          categoryId="category-1"
          tag={tag}
          isActive={false}
          onFormComplete={onComplete}
          onFormCancel={vi.fn()}
        />,
      ),
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Deactivate' }))

    // Assert
    expect(
      screen.getByText(/4 request\(s\) carry this tag and keep it/),
    ).toBeInTheDocument()
    expect(mockSetActive).toHaveBeenCalledWith({
      categoryId: 'category-1',
      tagId: 't-1',
      isActive: false,
    })
    expect(onComplete).toHaveBeenCalled()
  })

  it('activates an inactive tag', async () => {
    // Arrange
    await act(async () =>
      render(
        <ChangeTagActiveForm
          categoryId="category-1"
          tag={{ ...tag, isActive: false }}
          isActive
          onFormComplete={vi.fn()}
          onFormCancel={vi.fn()}
        />,
      ),
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Activate' }))

    // Assert
    expect(mockSetActive).toHaveBeenCalledWith({
      categoryId: 'category-1',
      tagId: 't-1',
      isActive: true,
    })
  })

  it('shows why the API refused and stays open', async () => {
    // Arrange
    mockSetActive.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 400,
          detail: 'The category is platform-defined.',
        }),
    })
    const onComplete = vi.fn()
    await act(async () =>
      render(
        <ChangeTagActiveForm
          categoryId="category-1"
          tag={tag}
          isActive={false}
          onFormComplete={onComplete}
          onFormCancel={vi.fn()}
        />,
      ),
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Deactivate' }))

    // Assert
    expect(mockError).toHaveBeenCalledWith('The category is platform-defined.')
    expect(onComplete).not.toHaveBeenCalled()
  })
})
