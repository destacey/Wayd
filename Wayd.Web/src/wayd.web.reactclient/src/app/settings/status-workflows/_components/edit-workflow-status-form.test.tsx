import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  StatusCategory,
  WorkflowAliasDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import EditWorkflowStatusForm from './edit-workflow-status-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// Not spread from the real barrel: it re-exports store-bound hooks. The stand-in
// keeps the hook's contract — a form that is open and submits its values.
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

const mockReclassify = jest.fn()
jest.mock('@/src/store/features/common/status-workflows-api', () => ({
  useRenameWorkflowStatusMutation: () => [jest.fn()],
  useReclassifyWorkflowStatusMutation: () => [mockReclassify],
}))

const ALIASES: WorkflowAliasDto[] = [
  {
    value: 2,
    name: 'Released',
    isRequired: true,
    category: StatusCategory.Done,
  },
  {
    value: 3,
    name: 'Ready',
    isRequired: true,
    category: StatusCategory.Active,
  },
]

const statusIn = (
  category: StatusCategory,
  alias: number,
): WorkflowStatusDto => ({
  id: 's-1',
  name: 'Shipped',
  category: { id: 1, name: category },
  alias,
  order: 1,
})

const renderForm = async (status: WorkflowStatusDto) =>
  await act(async () =>
    render(
      <EditWorkflowStatusForm
        workflowId="w-1"
        status={status}
        statuses={[status]}
        aliases={ALIASES}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const chooseCategory = async (category: StatusCategory) => {
  const field = screen.getByLabelText('Category')
  await act(() => userEvent.click(field))
  const options = await screen.findAllByTitle(category)
  await act(() => userEvent.click(options[options.length - 1]))
}

const meaningShown = () =>
  within(screen.getByLabelText('Meaning').closest('.ant-select') as HTMLElement)
    .getByTitle(/./)
    .getAttribute('title')

describe('EditWorkflowStatusForm', () => {
  beforeEach(() => {
    mockReclassify.mockReset()
  })

  it('opens showing the meaning the status holds', async () => {
    // Arrange / Act
    await renderForm(statusIn(StatusCategory.Done, 2))

    // Assert
    expect(meaningShown()).toBe('Released (required)')
  })

  it('drops the meaning on moving to a category it does not belong to, and brings it back on returning', async () => {
    // Arrange
    await renderForm(statusIn(StatusCategory.Done, 2))

    // Act
    await chooseCategory(StatusCategory.Active)
    const afterMove = meaningShown()
    await chooseCategory(StatusCategory.Done)

    // Assert
    expect(afterMove).toBe('None')
    expect(meaningShown()).toBe('Released (required)')
  })

  it('names a meaning the owner type no longer has and says to clear it', async () => {
    // Arrange / Act — a status cloned from a workflow whose meaning was retired
    await renderForm(statusIn(StatusCategory.Proposed, 5))

    // Assert
    expect(meaningShown()).toBe('Unknown meaning (5)')
    expect(
      screen.getByText(
        'This meaning no longer exists. Choose None or another meaning.',
      ),
    ).toBeInTheDocument()
  })

  it('names a meaning filed under the wrong category and warns about it', async () => {
    // Arrange / Act — a status cloned from a workflow saved before aliases were
    // tied to categories
    await renderForm(statusIn(StatusCategory.Active, 2))

    // Assert
    expect(meaningShown()).toBe('Released (belongs under Done)')
    expect(
      screen.getByText(
        'This meaning belongs under another category. Move the status there, or choose another meaning.',
      ),
    ).toBeInTheDocument()
  })
})
