import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeDeskEstimationScaleForm from './change-intake-desk-estimation-scale-form'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock('@/src/hooks', async () => {
  const { Form } = await vi.importActual<any>('antd')
  return {
    useModalForm: ({
      onSubmit,
      onComplete,
    }: {
      onSubmit: (
        values: Record<string, unknown>,
        form: unknown,
      ) => Promise<boolean>
      onComplete: () => void
    }) => {
      const [form] = Form.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          const values = await form.validateFields().catch(() => null)
          if (values && (await onSubmit(values, form))) onComplete()
        },
        handleCancel: vi.fn(),
      }
    },
  }
})

const mockChangeEstimationScale = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useChangeIntakeDeskEstimationScaleMutation: () => [mockChangeEstimationScale],
  useGetIntakeEstimationScaleChoicesQuery: () => ({
    data: [
      {
        id: 's-1',
        key: 1,
        name: 'T-Shirt Sizes',
        versionNumber: 2,
        estimateValues: ['S', 'M', 'L'],
      },
      {
        id: 's-2',
        key: 2,
        name: 'Fibonacci',
        versionNumber: 1,
        estimateValues: ['1', '2', '3', '5'],
      },
    ],
    isLoading: false,
  }),
}))

const renderForm = async (estimationScale?: { id: string; name: string }) =>
  await act(async () =>
    render(
      <ChangeIntakeDeskEstimationScaleForm
        desk={
          {
            id: 'd-1',
            name: 'IT',
            estimationScale: estimationScale && {
              ...estimationScale,
              key: 9,
              versionId: 'v-9',
              versionNumber: 1,
              isCurrentVersion: true,
            },
          } as any
        }
        onFormComplete={vi.fn()}
        onFormCancel={vi.fn()}
      />,
    ),
  )

const submit = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Change' }))
}

describe('ChangeIntakeDeskEstimationScaleForm', () => {
  beforeEach(() => {
    mockChangeEstimationScale.mockReset()
    mockChangeEstimationScale.mockReturnValue({
      unwrap: () => Promise.resolve(),
    })
  })

  it('names each scale with its values', async () => {
    // Arrange
    await renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert
    expect(await screen.findByText('S, M, L')).toBeInTheDocument()
    expect(screen.getByText('1, 2, 3, 5')).toBeInTheDocument()
  })

  it('gives the desk the chosen scale', async () => {
    // Arrange
    await renderForm()
    await userEvent.click(screen.getByRole('combobox'))
    await userEvent.click(await screen.findByTitle('T-Shirt Sizes'))

    // Act
    await submit()

    // Assert
    expect(mockChangeEstimationScale).toHaveBeenCalledWith({
      deskId: 'd-1',
      estimationScaleId: 's-1',
    })
  })

  it('stops estimating when no scale is chosen', async () => {
    // Act
    await renderForm()
    await submit()

    // Assert
    expect(mockChangeEstimationScale).toHaveBeenCalledWith({
      deskId: 'd-1',
      estimationScaleId: undefined,
    })
  })

  it('keeps an inactive scale the desk still estimates on showing by name', async () => {
    // Act
    await renderForm({ id: 's-old', name: 'Old Scale' })

    // Assert
    expect(screen.getByTitle('Old Scale (Inactive)')).toBeInTheDocument()
  })
})
