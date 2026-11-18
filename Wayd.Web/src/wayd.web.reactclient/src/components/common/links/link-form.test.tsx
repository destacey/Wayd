import type { UseModalFormOptions } from '@/src/hooks'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import LinkForm from './link-form'

const mockMessage = { success: vi.fn(), error: vi.fn() }
vi.mock('../../contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

// Submit through validateFields, as the real hook does, with a real form instance.
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

const mockCreate = vi.fn()
const mockUpdate = vi.fn()
vi.mock('@/src/store/features/common/links-api', () => ({
  useCreateLinkMutation: () => [mockCreate],
  useUpdateLinkMutation: () => [mockUpdate],
}))

const owner = { ownerType: 'ppm.project' as const, ownerId: 'project-1' }

const existing = {
  id: 'link-1',
  ownerType: owner.ownerType,
  ownerId: owner.ownerId,
  name: 'Charter',
  url: 'https://example.com/charter',
}

const fill = async (name: string, url: string) => {
  const nameInput = screen.getByLabelText('Name')
  const urlInput = screen.getByLabelText('URL')
  await userEvent.clear(nameInput)
  await userEvent.type(nameInput, name)
  await userEvent.clear(urlInput)
  await userEvent.type(urlInput, url)
}

const save = (label: string) =>
  userEvent.click(screen.getByRole('button', { name: label }))

describe('LinkForm', () => {
  const onFormComplete = vi.fn()

  beforeEach(() => {
    vi.clearAllMocks()
    mockCreate.mockResolvedValue({ data: 'link-2' })
    mockUpdate.mockResolvedValue({ data: null })
  })

  const renderForm = (link?: typeof existing) =>
    render(
      <LinkForm
        owner={owner}
        link={link}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )

  it('adds a link to the record it was opened for', async () => {
    renderForm()
    await fill('Wiki', 'https://example.com/wiki')

    await save('Create')

    expect(mockCreate).toHaveBeenCalledWith({
      ...owner,
      request: { name: 'Wiki', url: 'https://example.com/wiki' },
    })
    expect(mockUpdate).not.toHaveBeenCalled()
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('opens on the link it changes, and saves it under its id', async () => {
    renderForm(existing)
    expect(screen.getByLabelText('Name')).toHaveValue('Charter')
    expect(screen.getByLabelText('URL')).toHaveValue(
      'https://example.com/charter',
    )
    await fill('Project charter', 'https://example.com/v2')

    await save('Save')

    expect(mockUpdate).toHaveBeenCalledWith({
      ...owner,
      linkId: 'link-1',
      request: { name: 'Project charter', url: 'https://example.com/v2' },
    })
    expect(mockCreate).not.toHaveBeenCalled()
  })

  it('refuses an address that is not a web address before asking the server', async () => {
    renderForm()
    await fill('Wiki', 'not an address')

    await save('Create')

    expect(mockCreate).not.toHaveBeenCalled()
    expect(onFormComplete).not.toHaveBeenCalled()
  })

  it("marks the field the server's validation names, and stays open", async () => {
    mockCreate.mockResolvedValue({
      error: {
        status: 422,
        errors: { Url: ['A link needs a full web address.'] },
      },
    })
    renderForm()
    await fill('Wiki', 'https://example.com/wiki')

    await save('Create')

    expect(
      await screen.findByText('A link needs a full web address.'),
    ).toBeInTheDocument()
    expect(onFormComplete).not.toHaveBeenCalled()
  })

  it.each([
    [
      400,
      "'organization.nothing' is not a kind of record that can carry links.",
    ],
    [403, "You cannot change this record's links."],
  ])(
    "shows the server's reason for a %i, and stays open",
    async (status, detail) => {
      mockCreate.mockResolvedValue({ error: { status, detail } })
      renderForm()
      await fill('Wiki', 'https://example.com/wiki')

      await save('Create')

      expect(mockMessage.error).toHaveBeenCalledWith(detail)
      expect(onFormComplete).not.toHaveBeenCalled()
    },
  )

  it('says something when the failure carries no reason', async () => {
    mockUpdate.mockResolvedValue({ error: { status: 404 } })
    renderForm(existing)

    await save('Save')

    expect(mockMessage.error).toHaveBeenCalledWith(
      'An unexpected error occurred while updating the link.',
    )
  })
})
