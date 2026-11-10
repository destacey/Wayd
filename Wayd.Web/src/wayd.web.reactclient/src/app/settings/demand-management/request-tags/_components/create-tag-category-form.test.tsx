import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CreateTagCategoryForm from './create-tag-category-form'

const mockError = vi.fn()
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: vi.fn() }),
}))

// Form.useWatch does not refire under jsdom, so keep the form real and submit through validateFields.
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

const mockCreate = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useCreateIntakeRequestTagCategoryMutation: () => [mockCreate],
}))

const renderForm = async (onComplete = vi.fn()) => {
  await act(async () =>
    render(
      <CreateTagCategoryForm
        onFormComplete={onComplete}
        onFormCancel={vi.fn()}
      />,
    ),
  )
  return onComplete
}

describe('CreateTagCategoryForm', () => {
  beforeEach(() => {
    mockCreate.mockReset()
    mockError.mockReset()
    mockCreate.mockReturnValue({ unwrap: () => Promise.resolve('category-9') })
  })

  it('adds a single-tag category by default', async () => {
    // Arrange
    const onComplete = await renderForm()
    await userEvent.type(screen.getByLabelText('Name'), 'Area')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    // Assert
    expect(mockCreate).toHaveBeenCalledWith({
      name: 'Area',
      description: undefined,
      allowsMany: false,
    })
    expect(onComplete).toHaveBeenCalled()
  })

  it('adds a category that allows many tags when switched on', async () => {
    // Arrange
    await renderForm()
    await userEvent.type(screen.getByLabelText('Name'), 'Customer segment')
    await userEvent.click(screen.getByRole('switch'))

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

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
          detail: 'A tag category with this name already exists.',
        }),
    })
    const onComplete = await renderForm()
    await userEvent.type(screen.getByLabelText('Name'), 'Area')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    // Assert
    expect(mockError).toHaveBeenCalledWith(
      'A tag category with this name already exists.',
    )
    expect(onComplete).not.toHaveBeenCalled()
  })
})
