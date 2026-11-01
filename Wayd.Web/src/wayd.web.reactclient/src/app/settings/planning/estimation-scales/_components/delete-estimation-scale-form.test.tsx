import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import DeleteEstimationScaleForm from './delete-estimation-scale-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
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
jest.mock('@/src/store/features/planning/estimation-scales-api', () => ({
  useDeleteEstimationScaleMutation: () => [mockDelete],
}))

const scale = {
  id: '0b6f8c1e-5d7a-4f0e-9c3b-2a1d4e6f8a90',
  key: 7,
  name: 'Fibonacci',
}

describe('DeleteEstimationScaleForm', () => {
  beforeEach(() => {
    mockDelete.mockReset()
    mockDelete.mockResolvedValue({ data: undefined })
  })

  it('names the scale by its key, never its id', async () => {
    // Arrange / Act
    await act(async () =>
      render(
        <DeleteEstimationScaleForm
          estimationScale={scale}
          onFormComplete={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Assert
    expect(screen.getByText('7 - Fibonacci')).toBeInTheDocument()
    expect(screen.queryByText(new RegExp(scale.id))).not.toBeInTheDocument()
  })

  it('deletes the scale by its id', async () => {
    // Arrange
    const onComplete = jest.fn()
    await act(async () =>
      render(
        <DeleteEstimationScaleForm
          estimationScale={scale}
          onFormComplete={onComplete}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Delete' })),
    )

    // Assert
    expect(mockDelete).toHaveBeenCalledWith(scale.id)
    expect(onComplete).toHaveBeenCalled()
  })
})
