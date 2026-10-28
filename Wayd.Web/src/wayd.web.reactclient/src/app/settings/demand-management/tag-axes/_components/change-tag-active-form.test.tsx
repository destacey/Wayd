import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeTagActiveForm from './change-tag-active-form'

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

const mockSetActive = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useSetIntakeRequestTagActiveMutation: () => [mockSetActive],
  }),
)

const tag = { id: 't-1', name: 'Payments', isActive: true, requestCount: 4 }

describe('ChangeTagActiveForm', () => {
  beforeEach(() => {
    mockSetActive.mockReset()
    mockError.mockReset()
    mockSetActive.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('deactivates the tag, saying the requests carrying it keep it', async () => {
    // Arrange
    const onComplete = jest.fn()
    await act(async () =>
      render(
        <ChangeTagActiveForm
          axisId="axis-1"
          tag={tag}
          isActive={false}
          onFormComplete={onComplete}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Deactivate' })),
    )

    // Assert
    expect(
      screen.getByText(/4 request\(s\) carry this tag and keep it/),
    ).toBeInTheDocument()
    expect(mockSetActive).toHaveBeenCalledWith({
      categoryId: 'axis-1',
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
          axisId="axis-1"
          tag={{ ...tag, isActive: false }}
          isActive
          onFormComplete={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Activate' })),
    )

    // Assert
    expect(mockSetActive).toHaveBeenCalledWith({
      categoryId: 'axis-1',
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
          detail: 'The axis is platform-defined.',
        }),
    })
    const onComplete = jest.fn()
    await act(async () =>
      render(
        <ChangeTagActiveForm
          axisId="axis-1"
          tag={tag}
          isActive={false}
          onFormComplete={onComplete}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Deactivate' })),
    )

    // Assert
    expect(mockError).toHaveBeenCalledWith('The axis is platform-defined.')
    expect(onComplete).not.toHaveBeenCalled()
  })
})
