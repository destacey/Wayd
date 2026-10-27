import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CreateTagAxisForm from './create-tag-axis-form'

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
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useCreateIntakeRequestTagCategoryMutation: () => [mockCreate],
  }),
)

const renderForm = async (onComplete = jest.fn()) => {
  await act(async () =>
    render(
      <CreateTagAxisForm
        onFormComplete={onComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )
  return onComplete
}

describe('CreateTagAxisForm', () => {
  beforeEach(() => {
    mockCreate.mockReset()
    mockError.mockReset()
    mockCreate.mockReturnValue({ unwrap: () => Promise.resolve('axis-9') })
  })

  it('adds a single-tag axis by default', async () => {
    // Arrange
    const onComplete = await renderForm()
    await userEvent.type(screen.getByLabelText('Name'), 'Area')

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockCreate).toHaveBeenCalledWith({
      name: 'Area',
      description: undefined,
      allowsMany: false,
    })
    expect(onComplete).toHaveBeenCalled()
  })

  it('adds an axis that allows many tags when switched on', async () => {
    // Arrange
    await renderForm()
    await userEvent.type(screen.getByLabelText('Name'), 'Customer segment')
    await userEvent.click(screen.getByRole('switch'))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockCreate).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Customer segment', allowsMany: true }),
    )
  })

  it('shows why the API refused and stays open', async () => {
    // Arrange
    mockCreate.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 400,
          detail: 'A tag axis with this name already exists.',
        }),
    })
    const onComplete = await renderForm()
    await userEvent.type(screen.getByLabelText('Name'), 'Area')

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockError).toHaveBeenCalledWith(
      'A tag axis with this name already exists.',
    )
    expect(onComplete).not.toHaveBeenCalled()
  })
})
