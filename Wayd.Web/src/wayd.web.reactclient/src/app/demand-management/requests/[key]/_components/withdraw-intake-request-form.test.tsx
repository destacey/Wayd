import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import WithdrawIntakeRequestForm from './withdraw-intake-request-form'

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

const mockWithdraw = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useWithdrawIntakeRequestMutation: () => [mockWithdraw],
}))

const request = {
  summary: { id: 'r-42', key: 42, desk: { id: 'd-it', key: 1, name: 'IT' } },
  canWithdraw: true,
} as any

const renderForm = async (onFormComplete = vi.fn()) =>
  await act(async () =>
    render(
      <WithdrawIntakeRequestForm
        request={request}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    ),
  )

const submit = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Withdraw' }))
}

describe('WithdrawIntakeRequestForm', () => {
  beforeEach(() => {
    mockWithdraw.mockReset()
    mockWithdraw.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('withdraws with no note', async () => {
    // Arrange
    const onFormComplete = vi.fn()
    await renderForm(onFormComplete)

    // Act
    await submit()

    // Assert
    expect(mockWithdraw).toHaveBeenCalledWith({
      requestId: 'r-42',
      note: undefined,
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('withdraws with the note for the desk', async () => {
    // Arrange
    await renderForm()
    await userEvent.type(
      screen.getByRole('textbox', { name: 'Note' }),
      ' Solved it another way ',
    )

    // Act
    await submit()

    // Assert
    expect(mockWithdraw).toHaveBeenCalledWith({
      requestId: 'r-42',
      note: 'Solved it another way',
    })
  })

  it('names the desk the request is withdrawn from', async () => {
    // Act
    await renderForm()

    // Assert
    expect(screen.getByText(/tells the IT desk/)).toBeInTheDocument()
  })
})
