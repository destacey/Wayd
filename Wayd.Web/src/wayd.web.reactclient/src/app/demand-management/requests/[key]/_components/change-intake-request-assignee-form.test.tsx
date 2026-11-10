import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeRequestAssigneeForm from './change-intake-request-assignee-form'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

let mockEmployeeId: string | null = 'e-me'

// Keep the form instance real and submit through validateFields, as the real hook does.
vi.mock('@/src/hooks', async () => {
  const { Form } = await vi.importActual<any>('antd')
  return {
    useLinkedEmployee: () => ({
      employeeId: mockEmployeeId,
      hasLinkedEmployee: !!mockEmployeeId,
    }),
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

const mockChangeAssignee = vi.fn()
const mockDesk = {
  id: 'd-1',
  key: 3,
  name: 'Product delivery',
  members: [
    { employee: { id: 'e-me', key: 1, name: 'Morgan Me' }, role: 'Agent' },
    { employee: { id: 'e-7', key: 7, name: 'Casey Agent' }, role: 'Admin' },
  ],
}
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useChangeIntakeRequestAssigneeMutation: () => [mockChangeAssignee],
  useGetIntakeDeskQuery: () => ({ data: mockDesk, isLoading: false }),
}))

const request = (assignee?: { id: string; name: string }) =>
  ({
    summary: {
      id: 'r-42',
      key: 42,
      desk: { id: 'd-1', key: 3, name: 'Product delivery' },
      assignee,
    },
    canChangeAssignee: true,
  }) as any

const renderForm = async (r = request()) =>
  await act(async () =>
    render(
      <ChangeIntakeRequestAssigneeForm
        request={r}
        onFormComplete={vi.fn()}
        onFormCancel={vi.fn()}
      />,
    ),
  )

const save = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Save' }))
}

describe('ChangeIntakeRequestAssigneeForm', () => {
  beforeEach(() => {
    mockEmployeeId = 'e-me'
    mockChangeAssignee.mockReset()
    mockChangeAssignee.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('offers only the members of the request’s desk', async () => {
    // Arrange
    await renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert
    expect(await screen.findByTitle('Casey Agent')).toBeInTheDocument()
    expect(screen.getByTitle('Morgan Me')).toBeInTheDocument()
  })

  it('assigns the reader when they choose Assign to me', async () => {
    // Arrange
    await renderForm()

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Assign to me' }))
    await save()

    // Assert
    expect(mockChangeAssignee).toHaveBeenCalledWith({
      requestId: 'r-42',
      assigneeId: 'e-me',
    })
  })

  it('does not offer Assign to me to a reader who does not work the desk', async () => {
    // Arrange
    mockEmployeeId = 'e-outsider'

    // Act
    await renderForm()

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Assign to me' }),
    ).not.toBeInTheDocument()
  })

  it('unassigns when the assignee is cleared', async () => {
    // Arrange
    await renderForm(request({ id: 'e-7', name: 'Casey Agent' }))

    // Act
    await userEvent.click(document.querySelector('.ant-select-clear')!)
    await save()

    // Assert
    expect(mockChangeAssignee).toHaveBeenCalledWith({
      requestId: 'r-42',
      assigneeId: undefined,
    })
  })
})
