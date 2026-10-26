import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IntakeDeskRole, IntakeDeskVisibility } from '@/src/services/wayd-api'
import EditIntakeDeskMembersForm from './edit-intake-desk-members-form'

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

// The employee picker is an antd Select, which jsdom drives poorly; each stand-in picks the employees its
// test names, and shows whether it is disabled.
let mockPicks: Record<string, string[]> = {}
jest.mock('@/src/components/common/organizations', () => ({
  EmployeeSelect: ({
    id,
    disabled,
    onChange,
  }: {
    id: string
    disabled?: boolean
    onChange: (value: string[]) => void
  }) => {
    // Form.Item prefixes the field name with the form's name.
    const field = id.split('_').pop() ?? id
    return (
      <button
        type="button"
        disabled={disabled}
        onClick={() => onChange(mockPicks[field] ?? [])}
      >
        {`pick ${field}`}
      </button>
    )
  },
}))

const mockUpdate = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useUpdateIntakeDeskMutation: () => [mockUpdate],
    useGetIntakeEmployeeOptionsQuery: () => ({ data: [] }),
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
    revision: 4,
    ...overrides,
  }) as any

let rerenderWith: (d: ReturnType<typeof desk>) => void = () => undefined

const renderForm = async (d = desk()) =>
  await act(async () => {
    const element = (next: ReturnType<typeof desk>) => (
      <EditIntakeDeskMembersForm
        desk={next}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />
    )
    const view = render(element(d))
    rerenderWith = (next) => view.rerender(element(next))
  })

describe('EditIntakeDeskMembersForm', () => {
  beforeEach(() => {
    mockUpdate.mockReset()
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve() })
    mockPicks = {}
  })

  it('keeps Desk Admins out of reach of a Desk Admin', async () => {
    // Arrange / Act
    await renderForm(desk({ canAdminister: false }))

    // Assert
    expect(screen.getByText('pick adminIds')).toBeDisabled()
    expect(screen.getByText('pick agentIds')).toBeEnabled()
  })

  it('saves the agents a Desk Admin names, keeping the Desk Admins as they were', async () => {
    // Arrange
    mockPicks = { agentIds: ['e-2', 'e-3'] }
    await renderForm()
    await act(() => userEvent.click(screen.getByText('pick agentIds')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({
        expectedRevision: 4,
        members: [
          { employeeId: 'e-1', role: IntakeDeskRole.Admin },
          { employeeId: 'e-2', role: IntakeDeskRole.Agent },
          { employeeId: 'e-3', role: IntakeDeskRole.Agent },
        ],
      }),
    )
  })

  it('refuses to name someone as both a Desk Admin and an Agent', async () => {
    // Arrange
    mockPicks = { agentIds: ['e-1', 'e-2'] }
    await renderForm()
    await act(() => userEvent.click(screen.getByText('pick agentIds')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(
      await screen.findByText(
        'Someone holds one role on a desk: a Desk Admin already does everything an agent does.',
      ),
    ).toBeInTheDocument()
    expect(mockUpdate).not.toHaveBeenCalled()
  })

  it('keeps the revision it opened with when the page refetches the desk, so a retry stays refused', async () => {
    // Arrange — a failed save refetches the desk; the form must not adopt the newer revision with older values
    await renderForm(desk({ revision: 4 }))
    await act(async () =>
      rerenderWith(desk({ revision: 5, name: 'Changed by someone else' })),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ expectedRevision: 4 }),
    )
  })
})
