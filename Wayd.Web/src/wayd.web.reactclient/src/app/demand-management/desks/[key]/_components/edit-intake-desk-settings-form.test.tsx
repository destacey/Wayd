import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IntakeDeskRole, IntakeDeskVisibility } from '@/src/services/wayd-api'
import EditIntakeDeskSettingsForm from './edit-intake-desk-settings-form'

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

const mockUpdate = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useUpdateIntakeDeskMutation: () => [mockUpdate],
    useGetIntakeDeskVisibilitiesQuery: () => ({
      data: [
        {
          id: 1,
          code: 'Open',
          name: 'Open',
          description: 'Anyone reads it.',
          order: 1,
        },
        {
          id: 3,
          code: 'Private',
          name: 'Private',
          description: 'Only the desk.',
          order: 3,
        },
      ],
    }),
  }),
)

const desk = (overrides: Record<string, unknown> = {}) =>
  ({
    id: 'd-1',
    key: 1,
    name: 'IT',
    description: 'Laptops, access and software',
    visibility: IntakeDeskVisibility.Open,
    isOpen: true,
    isRequesterSelectable: true,
    requesterMayAddParticipants: true,
    members: [
      {
        employee: { id: 'e-1', key: 1, name: 'Avery' },
        role: IntakeDeskRole.Admin,
      },
      {
        employee: { id: 'e-2', key: 2, name: 'Blake' },
        role: IntakeDeskRole.Agent,
      },
    ],
    canManage: true,
    canAdminister: false,
    canDelete: false,
    ...overrides,
  }) as any

const renderForm = async (d = desk()) =>
  await act(async () =>
    render(
      <EditIntakeDeskSettingsForm
        desk={d}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

describe('EditIntakeDeskSettingsForm', () => {
  beforeEach(() => {
    mockUpdate.mockReset()
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('lets a Desk Admin see but not change the visibility', async () => {
    // Arrange / Act
    await renderForm(desk({ canAdminister: false }))

    // Assert
    expect(screen.getByRole('radio', { name: /Private/ })).toBeDisabled()
    expect(
      screen.getByText(
        'Only the Demand Management Admin can change who can see the requests on a desk.',
      ),
    ).toBeInTheDocument()
  })

  it('lets the Demand Management Admin change the visibility', async () => {
    // Arrange / Act
    await renderForm(desk({ canAdminister: true }))

    // Assert
    expect(screen.getByRole('radio', { name: /Private/ })).toBeEnabled()
  })

  it('saves the changed settings with the desk otherwise unchanged', async () => {
    // Arrange
    await renderForm()
    const name = screen.getByLabelText('Name')
    await userEvent.clear(name)
    await userEvent.type(name, 'IT services')

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith({
      id: 'd-1',
      name: 'IT services',
      description: 'Laptops, access and software',
      visibility: IntakeDeskVisibility.Open,
      isRequesterSelectable: true,
      isOpen: true,
      requesterMayAddParticipants: true,
      members: [
        { employeeId: 'e-1', role: IntakeDeskRole.Admin },
        { employeeId: 'e-2', role: IntakeDeskRole.Agent },
      ],
    })
  })
})
