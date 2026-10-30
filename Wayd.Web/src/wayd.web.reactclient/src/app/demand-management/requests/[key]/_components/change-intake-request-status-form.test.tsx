import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { StatusCategory } from '@/src/services/wayd-api'
import ChangeIntakeRequestStatusForm from './change-intake-request-status-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// In Jest, Form.useWatch does not refire after typing, so keep the form instance real and submit through
// validateFields, which, like the real hook, returns only the fields that are mounted.
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

const option = (
  id: string,
  name: string,
  overrides: Record<string, unknown> = {},
) => ({
  id,
  name,
  category: StatusCategory.Proposed,
  alias: 0,
  needsReason: false,
  needsSurvivingRequest: false,
  ...overrides,
})

const mockChangeStatus = jest.fn()
const mockOptions = [
  option('s-review', 'In Review'),
  option('s-waiting', 'Waiting on Requester', { alias: 2 }),
  option('s-declined', 'Declined', {
    category: StatusCategory.Removed,
    needsReason: true,
  }),
  option('s-duplicate', 'Duplicate', {
    category: StatusCategory.Removed,
    needsReason: true,
    needsSurvivingRequest: true,
  }),
]
const mockRequests = [
  { id: 'r-42', key: 42, title: 'This one', desk: { id: 'd-it' } },
  { id: 'r-7', key: 7, title: 'Export invoices', desk: { id: 'd-it' } },
  { id: 'r-9', key: 9, title: 'Board pack', desk: { id: 'd-secret' } },
]
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useChangeIntakeRequestStatusMutation: () => [mockChangeStatus],
    useGetIntakeRequestStatusOptionsQuery: () => ({
      data: mockOptions,
      isLoading: false,
      isError: false,
    }),
    useGetIntakeRequestsQuery: () => ({ data: mockRequests }),
  }),
)

const request = {
  summary: {
    id: 'r-42',
    key: 42,
    desk: { id: 'd-it', key: 1, name: 'IT' },
    status: { id: 's-submitted', name: 'Submitted', category: 'Proposed' },
  },
  canChangeStatus: true,
} as any

const renderForm = async () =>
  await act(async () =>
    render(
      <ChangeIntakeRequestStatusForm
        request={request}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const choose = async (comboboxIndex: number, title: string) => {
  await userEvent.click(screen.getAllByRole('combobox')[comboboxIndex])
  await userEvent.click(await screen.findByTitle(title))
}

const submit = async () => {
  await act(() =>
    userEvent.click(screen.getByRole('button', { name: 'Change' })),
  )
}

describe('ChangeIntakeRequestStatusForm', () => {
  beforeEach(() => {
    mockChangeStatus.mockReset()
    mockChangeStatus.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('moves to a status that needs nothing more, without a reason', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'In Review')

    // Act
    await submit()

    // Assert
    expect(mockChangeStatus).toHaveBeenCalledWith({
      requestId: 'r-42',
      statusId: 's-review',
      reason: undefined,
      duplicateOfId: undefined,
    })
  })

  it('asks the requester a question with a move to Waiting on Requester, in place of a reason', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'Waiting on Requester')
    await userEvent.type(
      screen.getByRole('textbox', { name: 'Question for the Requester' }),
      '  Could you add the budget? ',
    )

    // Act
    await submit()

    // Assert
    expect(screen.queryByRole('textbox', { name: 'Reason' })).toBeNull()
    expect(mockChangeStatus).toHaveBeenCalledWith(
      expect.objectContaining({
        statusId: 's-waiting',
        reply: { body: 'Could you add the budget?' },
      }),
    )
  })

  it('moves to Waiting on Requester without a reply when no question is written', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'Waiting on Requester')

    // Act
    await submit()

    // Assert
    expect(mockChangeStatus).toHaveBeenCalledWith(
      expect.objectContaining({ statusId: 's-waiting', reply: undefined }),
    )
  })

  it('refuses to decline without a reason', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'Declined')

    // Act
    await submit()

    // Assert — jsdom never runs the error message's entry animation, so the field's invalid state stands for it
    expect(mockChangeStatus).not.toHaveBeenCalled()
    expect(screen.getByRole('textbox', { name: 'Reason' })).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  it('declines with the reason the requester will read', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'Declined')
    await userEvent.type(
      screen.getByRole('textbox', { name: 'Reason' }),
      '  Budget went elsewhere ',
    )

    // Act
    await submit()

    // Assert
    expect(mockChangeStatus).toHaveBeenCalledWith(
      expect.objectContaining({
        statusId: 's-declined',
        reason: 'Budget went elsewhere',
      }),
    )
  })

  it('asks which request a duplicate gives way to, offering every other request', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'Duplicate')
    await userEvent.type(
      screen.getByRole('textbox', { name: 'Reason' }),
      'Asked already',
    )

    // Act
    await submit()

    // Assert
    expect(mockChangeStatus).not.toHaveBeenCalled()
    const survivor = screen.getAllByRole('combobox')[1]
    expect(survivor).toHaveAttribute('aria-invalid', 'true')
    await userEvent.click(survivor)
    expect(await screen.findByTitle('7 · Export invoices')).toBeInTheDocument()
    expect(screen.queryByTitle('42 · This one')).not.toBeInTheDocument()
  })

  it('marks a duplicate of the chosen request', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'Duplicate')
    await choose(1, '7 · Export invoices')
    await userEvent.type(
      screen.getByRole('textbox', { name: 'Reason' }),
      'Asked already',
    )

    // Act
    await submit()

    // Assert
    expect(mockChangeStatus).toHaveBeenCalledWith({
      requestId: 'r-42',
      statusId: 's-duplicate',
      reason: 'Asked already',
      duplicateOfId: 'r-7',
    })
  })

  it('clears the reason error once a status that needs no reason is chosen', async () => {
    // Arrange
    await renderForm()
    await choose(0, 'Declined')
    await submit()
    expect(screen.getByRole('textbox', { name: 'Reason' })).toHaveAttribute(
      'aria-invalid',
      'true',
    )

    // Act
    await choose(0, 'In Review')

    // Assert
    expect(screen.getByRole('textbox', { name: 'Reason' })).not.toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  it('puts a field error from the server on the field it names', async () => {
    // Arrange
    mockChangeStatus.mockReturnValue({
      unwrap: () =>
        Promise.reject({ status: 422, errors: { Reason: ['Too long.'] } }),
    })
    await renderForm()
    await choose(0, 'In Review')

    // Act
    await submit()

    // Assert
    expect(screen.getByRole('textbox', { name: 'Reason' })).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })
})
