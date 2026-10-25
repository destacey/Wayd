import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IntakeDeskVisibility } from '@/src/services/wayd-api'
import RerouteIntakeRequestForm, {
  wideningNotice,
} from './reroute-intake-request-form'

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

const desk = (
  id: string,
  name: string,
  visibility: IntakeDeskVisibility,
  overrides: Record<string, unknown> = {},
) => ({
  id,
  key: 1,
  name,
  visibility,
  isOpen: true,
  members: [],
  ...overrides,
})

const mockReroute = jest.fn()
const mockDesks = [
  desk('d-private', 'Security', IntakeDeskVisibility.Private),
  desk('d-open', 'Product delivery', IntakeDeskVisibility.Open, {
    members: [{ employee: { id: 'e-7', key: 7, name: 'Casey Agent' } }],
  }),
  desk('d-closed', 'Retired desk', IntakeDeskVisibility.Private, {
    isOpen: false,
  }),
  desk('d-other-private', 'Legal', IntakeDeskVisibility.Private),
]
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useRerouteIntakeRequestMutation: () => [mockReroute],
    useGetIntakeDesksQuery: () => ({ data: mockDesks }),
  }),
)

const request = (assignee?: { id: string; name: string }) =>
  ({
    summary: {
      id: 'r-42',
      key: 42,
      desk: { id: 'd-private', key: 1, name: 'Security' },
      assignee,
    },
    canReroute: true,
  }) as any

const renderForm = async (r = request()) =>
  await act(async () =>
    render(
      <RerouteIntakeRequestForm
        request={r}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const chooseDesk = async (name: string) => {
  await userEvent.click(screen.getByRole('combobox'))
  await userEvent.click(await screen.findByTitle(name))
}

const submit = async () => {
  await act(() => userEvent.click(screen.getByRole('button', { name: 'Move' })))
}

describe('wideningNotice', () => {
  it.each([
    [IntakeDeskVisibility.Open, IntakeDeskVisibility.Private],
    [IntakeDeskVisibility.SummaryOnly, IntakeDeskVisibility.Private],
    [IntakeDeskVisibility.Private, IntakeDeskVisibility.Private],
  ])('says nothing for a move from %s to %s', (from, to) => {
    // Act
    const notice = wideningNotice(from, to)

    // Assert
    expect(notice).toBeNull()
  })

  it('names the summary for a private request reaching a summary-only desk', () => {
    // Act
    const notice = wideningNotice(
      IntakeDeskVisibility.Private,
      IntakeDeskVisibility.SummaryOnly,
    )

    // Assert
    expect(notice).toBe(
      'Everyone who can view requests will see its title, requester, channel and need-by date.',
    )
  })

  it('names the summary and the detail for a private request reaching an open desk', () => {
    // Act
    const notice = wideningNotice(
      IntakeDeskVisibility.Private,
      IntakeDeskVisibility.Open,
    )

    // Assert
    expect(notice).toBe(
      'Everyone who can view requests will see its title, requester, channel and need-by date, and its description and expected benefits.',
    )
  })

  it('names only the detail for a summary-only request reaching an open desk', () => {
    // Act
    const notice = wideningNotice(
      IntakeDeskVisibility.SummaryOnly,
      IntakeDeskVisibility.Open,
    )

    // Assert
    expect(notice).toBe(
      'Everyone who can view requests will see its description and expected benefits.',
    )
  })
})

describe('RerouteIntakeRequestForm', () => {
  beforeEach(() => {
    mockReroute.mockReset()
    mockReroute.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('offers only other desks that are open to new requests', async () => {
    // Arrange
    await renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert
    expect(await screen.findByTitle('Product delivery')).toBeInTheDocument()
    expect(screen.queryByTitle('Retired desk')).not.toBeInTheDocument()
    expect(screen.queryByTitle('Security')).not.toBeInTheDocument()
  })

  it('moves to an equally private desk without asking for confirmation', async () => {
    // Arrange
    await renderForm()
    await chooseDesk('Legal')

    // Act
    await submit()

    // Assert
    expect(screen.queryByText(/is less private than/)).not.toBeInTheDocument()
    expect(mockReroute).toHaveBeenCalledWith({
      requestId: 'r-42',
      deskId: 'd-other-private',
      confirmWiderVisibility: false,
    })
  })

  it('refuses a move to a less private desk until it is confirmed', async () => {
    // Arrange
    await renderForm()
    await chooseDesk('Product delivery')

    // Act
    await submit()

    // Assert
    expect(
      screen.getByText('Product delivery is less private than Security'),
    ).toBeInTheDocument()
    // jsdom never runs the error message's entry animation, so the field's invalid state stands for it.
    expect(mockReroute).not.toHaveBeenCalled()
    expect(
      screen.getByRole('checkbox', {
        name: 'I understand this makes the request visible to more people',
      }),
    ).toHaveAttribute('aria-invalid', 'true')
  })

  it('sends the confirmation once the mover gives it', async () => {
    // Arrange
    await renderForm()
    await chooseDesk('Product delivery')
    await userEvent.click(
      screen.getByRole('checkbox', {
        name: 'I understand this makes the request visible to more people',
      }),
    )

    // Act
    await submit()

    // Assert
    expect(mockReroute).toHaveBeenCalledWith({
      requestId: 'r-42',
      deskId: 'd-open',
      confirmWiderVisibility: true,
    })
  })

  it('warns that an assignee who does not work the new desk will be unassigned', async () => {
    // Arrange
    await renderForm(request({ id: 'e-1', name: 'Avery Owner' }))

    // Act
    await chooseDesk('Legal')

    // Assert
    expect(
      screen.getByText(
        'Avery Owner does not work Legal, so the request will be unassigned.',
      ),
    ).toBeInTheDocument()
  })
})
