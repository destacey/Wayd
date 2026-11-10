import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeDeskScoringModelForm from './change-intake-desk-scoring-model-form'

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

const mockChangeScoringModel = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useChangeIntakeDeskScoringModelMutation: () => [mockChangeScoringModel],
  useGetIntakeScoringModelChoicesQuery: () => ({
    data: [
      { id: 'm-1', key: 1, name: 'WSJF', description: '' },
      { id: 'm-2', key: 2, name: 'Weighted Criteria', description: '' },
    ],
    isLoading: false,
  }),
}))

const renderForm = async (scoringModel?: { id: string; name: string }) =>
  await act(async () =>
    render(
      <ChangeIntakeDeskScoringModelForm
        desk={
          {
            id: 'd-1',
            name: 'IT',
            scoringModel: scoringModel && { ...scoringModel, key: 9 },
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

describe('ChangeIntakeDeskScoringModelForm', () => {
  beforeEach(() => {
    mockChangeScoringModel.mockReset()
    mockChangeScoringModel.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('gives the desk the chosen model', async () => {
    // Arrange
    await renderForm()
    await userEvent.click(screen.getByRole('combobox'))
    await userEvent.click(await screen.findByTitle('WSJF'))

    // Act
    await submit()

    // Assert
    expect(mockChangeScoringModel).toHaveBeenCalledWith({
      deskId: 'd-1',
      scoringModelId: 'm-1',
    })
  })

  it('stops scoring when no model is chosen', async () => {
    // Act
    await renderForm()
    await submit()

    // Assert
    expect(mockChangeScoringModel).toHaveBeenCalledWith({
      deskId: 'd-1',
      scoringModelId: undefined,
    })
  })

  it('keeps an archived model the desk still scores with showing by name', async () => {
    // Act
    await renderForm({ id: 'm-old', name: 'Old Model' })

    // Assert
    expect(screen.getByTitle('Old Model (Archived)')).toBeInTheDocument()
  })
})
