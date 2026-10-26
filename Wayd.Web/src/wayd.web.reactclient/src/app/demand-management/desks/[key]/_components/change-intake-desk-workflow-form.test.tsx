import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeDeskWorkflowForm from './change-intake-desk-workflow-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
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
        handleCancel: jest.fn(),
      }
    },
  }
})

const mockChangeWorkflow = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useChangeIntakeDeskWorkflowMutation: () => [mockChangeWorkflow],
    useGetIntakeWorkflowChoicesQuery: () => ({
      data: [
        { id: 'w-1', key: 3, name: 'Default Intake Workflow' },
        { id: 'w-2', key: 4, name: 'Board Review' },
      ],
      isLoading: false,
    }),
  }),
)

const renderForm = async () =>
  await act(async () =>
    render(
      <ChangeIntakeDeskWorkflowForm
        deskId="d-1"
        deskName="IT"
        workflow={
          {
            workflow: { id: 'w-1', key: 3, name: 'Default Intake Workflow' },
            followsOrganizationDefault: true,
            hasRequests: false,
            canChange: true,
          } as any
        }
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const submit = async () => {
  await act(() =>
    userEvent.click(screen.getByRole('button', { name: 'Change' })),
  )
}

describe('ChangeIntakeDeskWorkflowForm', () => {
  beforeEach(() => {
    mockChangeWorkflow.mockReset()
    mockChangeWorkflow.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('starts at the workflow the desk follows', async () => {
    // Act
    await renderForm()

    // Assert
    expect(screen.getByTitle('Default Intake Workflow')).toBeInTheDocument()
  })

  it('points the desk at the chosen workflow', async () => {
    // Arrange
    await renderForm()
    await userEvent.click(screen.getByRole('combobox'))
    await userEvent.click(await screen.findByTitle('Board Review'))

    // Act
    await submit()

    // Assert
    expect(mockChangeWorkflow).toHaveBeenCalledWith({
      deskId: 'd-1',
      workflowId: 'w-2',
    })
  })
})
