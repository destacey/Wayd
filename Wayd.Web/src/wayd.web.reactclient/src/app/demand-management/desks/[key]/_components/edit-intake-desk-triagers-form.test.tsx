import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IntakeDeskRole, IntakeDeskVisibility } from '@/src/services/wayd-api'
import EditIntakeDeskTriagersForm from './edit-intake-desk-triagers-form'
import type { UseModalFormOptions } from '@/src/hooks'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

// Keep the form instance real and submit through validateFields, as the real hook does.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
    useModalForm: <TValues,>({
      onSubmit,
      onComplete,
    }: UseModalFormOptions<TValues>) => {
      const [form] = Form.useForm<TValues>()
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

const mockUpdate = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useUpdateIntakeDeskMutation: () => [mockUpdate],
}))

const desk = (triagers: string[] = ['e-1']) =>
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
        isTriager: triagers.includes('e-1'),
      },
      {
        employee: { id: 'e-2', key: 2, name: 'Blake' },
        role: IntakeDeskRole.Agent,
        isTriager: triagers.includes('e-2'),
      },
    ],
    canManage: true,
    canAdminister: false,
    canDelete: false,
    revision: 4,
  }) as any

const renderForm = async (d = desk()) => {
  let container: HTMLElement = document.body
  await act(async () => {
    container = render(
      <EditIntakeDeskTriagersForm
        desk={d}
        onFormComplete={vi.fn()}
        onFormCancel={vi.fn()}
      />,
    ).baseElement
  })
  return container
}

const moveButtons = (container: HTMLElement) =>
  Array.from(
    container.querySelectorAll<HTMLButtonElement>(
      '.ant-transfer-actions button',
    ),
  )

describe('EditIntakeDeskTriagersForm', () => {
  beforeEach(() => {
    mockUpdate.mockReset()
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('saves an agent moved into triage beside the triagers the desk had', async () => {
    // Arrange
    const container = await renderForm()
    await userEvent.click(screen.getByText('Blake'))
    await userEvent.click(moveButtons(container)[0])

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({
        expectedRevision: 4,
        triagerIds: expect.arrayContaining(['e-1', 'e-2']),
      }),
    )
  })

  it('saves nobody when every triager is moved out', async () => {
    // Arrange
    const container = await renderForm()
    await userEvent.click(screen.getByText('Avery'))
    await userEvent.click(moveButtons(container)[1])

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ triagerIds: [] }),
    )
  })

  it('does not warn while someone triages', async () => {
    // Arrange / Act
    await renderForm(desk(['e-1']))

    // Assert
    expect(
      screen.queryByText(
        'Nobody is told about new requests; they are triaged from the board.',
      ),
    ).toBeNull()
  })

  it('warns when the desk has nobody triaging', async () => {
    // Arrange / Act
    await renderForm(desk([]))

    // Assert
    expect(
      screen.getByText(
        'Nobody is told about new requests; they are triaged from the board.',
      ),
    ).toBeInTheDocument()
  })
})
