import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import IntakeChannelForm from './intake-channel-form'

const mockError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: jest.fn() }),
}))

// Form.useWatch does not refire in Jest, so keep the form real and submit through validateFields.
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

const mockCreate = jest.fn()
const mockUpdate = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useCreateIntakeChannelMutation: () => [mockCreate],
    useUpdateIntakeChannelMutation: () => [mockUpdate],
  }),
)

const channel = {
  id: 'c-1',
  name: 'Support',
  description: 'Raised from support cases.',
  order: 3,
  isActive: true,
}

describe('IntakeChannelForm', () => {
  beforeEach(() => {
    mockCreate.mockReset()
    mockUpdate.mockReset()
    mockError.mockReset()
  })

  it('adds a channel, sending no description when none is given', async () => {
    // Arrange
    mockCreate.mockReturnValue({ unwrap: () => Promise.resolve('c-9') })
    const onComplete = jest.fn()
    await act(async () =>
      render(
        <IntakeChannelForm
          onFormComplete={onComplete}
          onFormCancel={jest.fn()}
        />,
      ),
    )
    await userEvent.type(screen.getByLabelText('Name'), 'Partner')

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockCreate).toHaveBeenCalledWith({
      name: 'Partner',
      description: undefined,
    })
    expect(onComplete).toHaveBeenCalled()
  })

  it('opens on the channel being edited and saves its changes', async () => {
    // Arrange
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve() })
    await act(async () =>
      render(
        <IntakeChannelForm
          channel={channel}
          onFormComplete={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      ),
    )
    const name = screen.getByLabelText('Name')
    expect(name).toHaveValue('Support')
    await userEvent.clear(name)
    await userEvent.type(name, 'Customer support')

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith({
      id: 'c-1',
      name: 'Customer support',
      description: 'Raised from support cases.',
    })
  })

  it('shows why the API refused a change and stays open', async () => {
    // Arrange
    mockUpdate.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 400,
          detail: 'A channel with this name already exists.',
        }),
    })
    const onComplete = jest.fn()
    await act(async () =>
      render(
        <IntakeChannelForm
          channel={channel}
          onFormComplete={onComplete}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockError).toHaveBeenCalledWith(
      'A channel with this name already exists.',
    )
    expect(onComplete).not.toHaveBeenCalled()
  })
})
