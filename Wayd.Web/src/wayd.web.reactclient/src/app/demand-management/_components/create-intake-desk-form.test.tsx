import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IntakeDeskVisibility } from '@/src/services/wayd-api'
import CreateIntakeDeskForm from './create-intake-desk-form'

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => true, hasClaim: () => true }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// In Jest, Form.useWatch does not refire after typing, so the real hook's validity never updates; as the
// other form suites do, keep the form instance real and submit straight through.
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
          if (await onSubmit(form.getFieldsValue(true), form)) onComplete()
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
    useCreateIntakeDeskMutation: () => [mockCreate],
    useGetIntakeDeskVisibilitiesQuery: () => ({
      data: [
        {
          id: 1,
          code: 'Open',
          name: 'Open',
          description: 'Anyone who can view requests reads the whole request.',
          order: 1,
        },
        {
          id: 3,
          code: 'Private',
          name: 'Private',
          description:
            "Only the desk and the request's own people know the request exists.",
          order: 3,
        },
      ],
    }),
    useGetIntakeEmployeeOptionsQuery: () => ({ data: [] }),
  }),
)

const renderForm = async (onFormComplete = jest.fn()) =>
  await act(async () =>
    render(
      <CreateIntakeDeskForm
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )

describe('CreateIntakeDeskForm', () => {
  beforeEach(() => {
    mockCreate.mockReset()
  })

  it('offers the visibilities from the lookup, with what each exposes', async () => {
    // Arrange / Act
    await renderForm()

    // Assert
    expect(screen.getByText('Open')).toBeInTheDocument()
    expect(
      screen.getByText(
        "Only the desk and the request's own people know the request exists.",
      ),
    ).toBeInTheDocument()
  })

  it('asks whether requesters may add participants only for a private desk', async () => {
    // Arrange
    await renderForm()
    expect(
      screen.queryByText('Requesters may add participants'),
    ).not.toBeInTheDocument()

    // Act
    await act(() => userEvent.click(screen.getByText('Private')))

    // Assert
    expect(
      await screen.findByText('Requesters may add participants'),
    ).toBeInTheDocument()
  })

  it('creates the desk with what was entered', async () => {
    // Arrange
    mockCreate.mockReturnValue({
      unwrap: () => Promise.resolve({ id: 'd-1', key: 1 }),
    })
    const onFormComplete = jest.fn()
    await renderForm(onFormComplete)
    await userEvent.type(screen.getByLabelText('Name'), 'IT')
    await userEvent.type(
      screen.getByLabelText('What to bring here'),
      'Laptops, access and software',
    )
    await act(() => userEvent.click(screen.getByText('Open')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Create' })),
    )

    // Assert
    expect(mockCreate).toHaveBeenCalledWith({
      name: 'IT',
      description: 'Laptops, access and software',
      visibility: IntakeDeskVisibility.Open,
      isRequesterSelectable: true,
      isOpen: true,
      requesterMayAddParticipants: true,
      reviewerIds: [],
    })
    expect(onFormComplete).toHaveBeenCalled()
  })
})
