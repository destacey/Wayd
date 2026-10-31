import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeDiscussionSettingsForm from './change-discussion-settings-form'

const mockMessage = { error: jest.fn(), success: jest.fn(), warning: jest.fn() }
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

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

const mockSaveSettings = jest.fn()
jest.mock('@/src/store/features/common/discussions-api', () => ({
  useSaveDiscussionSettingsMutation: () => [mockSaveSettings],
}))

const deskSettings = {
  channelType: 'demand.intake-request',
  scopeId: 'd-1',
  internalNotesEnabled: true,
  mentionsGrantAccess: true,
  isInherited: true,
  supportsInternalNotes: true,
  supportsMentionAccess: true,
  canManage: true,
}

const mockOnFormComplete = jest.fn()

const renderForm = async (settings = deskSettings) =>
  await act(async () =>
    render(
      <ChangeDiscussionSettingsForm
        settings={settings as any}
        scopeName="IT Help Desk"
        onFormComplete={mockOnFormComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const failWith = (error: { status: number; detail?: string }) =>
  mockSaveSettings.mockReturnValue({ unwrap: () => Promise.reject(error) })

const submit = async () => {
  await act(() => userEvent.click(screen.getByRole('button', { name: 'Save' })))
}

describe('ChangeDiscussionSettingsForm', () => {
  beforeEach(() => {
    mockSaveSettings.mockReset()
    mockOnFormComplete.mockReset()
    Object.values(mockMessage).forEach((fn) => fn.mockReset())
    mockSaveSettings.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('saves the settings as shown', async () => {
    await renderForm()

    await submit()

    expect(mockSaveSettings).toHaveBeenCalledWith({
      channelType: 'demand.intake-request',
      scopeId: 'd-1',
      internalNotesEnabled: true,
      mentionsGrantAccess: true,
    })
  })

  it('saves internal notes turned off', async () => {
    await renderForm()

    await act(() =>
      userEvent.click(screen.getByRole('switch', { name: 'Internal Notes' })),
    )
    await submit()

    expect(mockSaveSettings).toHaveBeenCalledWith(
      expect.objectContaining({ internalNotesEnabled: false }),
    )
  })

  it('offers only the settings the kind of record supports', async () => {
    await renderForm({
      ...deskSettings,
      internalNotesEnabled: false,
      mentionsGrantAccess: false,
      supportsInternalNotes: false,
      supportsMentionAccess: false,
    })

    expect(screen.queryByRole('switch', { name: 'Internal Notes' })).toBeNull()
    expect(
      screen.queryByRole('switch', { name: 'Mentions Give Access' }),
    ).toBeNull()
    await submit()

    expect(mockSaveSettings).toHaveBeenCalledWith(
      expect.objectContaining({
        internalNotesEnabled: false,
        mentionsGrantAccess: false,
      }),
    )
  })

  it('saves mentions turned off', async () => {
    await renderForm()

    await act(() =>
      userEvent.click(
        screen.getByRole('switch', { name: 'Mentions Give Access' }),
      ),
    )
    await submit()

    expect(mockSaveSettings).toHaveBeenCalledWith(
      expect.objectContaining({ mentionsGrantAccess: false }),
    )
  })

  it('stays open and says why when the save is refused', async () => {
    failWith({ status: 403, detail: 'You cannot change these settings.' })
    await renderForm()

    await submit()

    expect(mockMessage.error).toHaveBeenCalledWith(
      'You cannot change these settings.',
    )
    expect(mockOnFormComplete).not.toHaveBeenCalled()
  })

  it('closes when someone else saved first, so their change is not saved over unseen', async () => {
    failWith({ status: 409 })
    await renderForm()

    await submit()

    expect(mockMessage.warning).toHaveBeenCalled()
    expect(mockOnFormComplete).toHaveBeenCalled()
  })
})
