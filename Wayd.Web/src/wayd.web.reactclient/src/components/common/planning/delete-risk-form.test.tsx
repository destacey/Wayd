import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { RiskDetailsDto } from '@/src/services/wayd-api'
import DeleteRiskForm from './delete-risk-form'

const mockMessageSuccess = jest.fn()
const mockMessageError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: mockMessageSuccess,
    error: mockMessageError,
  }),
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
jest.mock('@/src/store/features/planning/risks-api', () => ({
  useDeleteRiskMutation: () => [mockDelete],
}))

const risk = {
  id: '3c9e2f7a-1b4d-4e8f-a6c2-5d7b9e0f1a23',
  key: 314,
  summary: 'Vendor API sunset before cutover',
} as RiskDetailsDto

describe('DeleteRiskForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockDelete.mockResolvedValue({ data: undefined })
  })

  it('names the risk by its key and summary, never its id', async () => {
    // Arrange / Act
    await act(async () =>
      render(
        <DeleteRiskForm
          risk={risk}
          onFormSave={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Assert
    expect(
      screen.getByText('314 - Vendor API sunset before cutover'),
    ).toBeInTheDocument()
    expect(screen.queryByText(new RegExp(risk.id))).not.toBeInTheDocument()
  })

  it('deletes the risk by its id and reports success', async () => {
    // Arrange
    const onFormSave = jest.fn()
    await act(async () =>
      render(
        <DeleteRiskForm
          risk={risk}
          onFormSave={onFormSave}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Delete' })),
    )

    // Assert
    expect(mockDelete).toHaveBeenCalledWith(risk.id)
    expect(onFormSave).toHaveBeenCalled()
    expect(mockMessageSuccess).toHaveBeenCalledWith(
      'Successfully deleted risk.',
    )
  })

  it('stays open and shows the API error when the delete fails', async () => {
    // Arrange
    mockDelete.mockResolvedValue({
      error: { status: 404, detail: 'Risk 314 not found.' },
    })
    const onFormSave = jest.fn()
    await act(async () =>
      render(
        <DeleteRiskForm
          risk={risk}
          onFormSave={onFormSave}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Delete' })),
    )

    // Assert
    expect(mockMessageError).toHaveBeenCalledWith('Risk 314 not found.')
    expect(onFormSave).not.toHaveBeenCalled()
  })
})
