import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { VersionDto } from '@/src/services/wayd-api'
import EditVersionForm from './edit-version-form'

const version = {
  id: 'version-1',
  key: 4,
  product: { id: 'product-1', key: 7, name: 'Wayd API' },
  version: '4.8.2',
  name: 'Spring hardening',
  // Set by an import. Nothing in this form can edit it.
  sequence: 30,
  status: { id: 'status-1', name: 'Planned', category: 'Proposed', alias: 0 },
} as unknown as VersionDto

const updateVersion = jest.fn().mockResolvedValue({ data: undefined })

jest.mock('@/src/components/common/markdown', () => ({
  MarkdownEditor: () => <textarea data-testid="markdown-editor" />,
}))

// Ships as untranspiled ESM, which Jest cannot parse from node_modules.
jest.mock('antd/es/input/TextArea', () => ({
  __esModule: true,
  default: (props: Record<string, unknown>) => <textarea {...props} />,
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// Not spread from the real barrel: it re-exports store-bound hooks, and pulling
// those in initialises a store this form never touches. The form instance is
// real, though — antd's Form binds to it, and a stub breaks on render.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
    }: {
      onSubmit: (
        values: Record<string, unknown>,
        form: unknown,
      ) => Promise<boolean>
    }) => {
      const [form] = Form.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => await onSubmit(form.getFieldsValue(), form),
        handleCancel: jest.fn(),
      }
    },
  }
})

jest.mock('@/src/store/features/product-management/versions-api', () => ({
  useUpdateVersionMutation: () => [updateVersion],
}))

describe('EditVersionForm', () => {
  beforeEach(() => updateVersion.mockClear())

  it('does not ask for the dates', () => {
    // Each date carries a rule the aggregate enforces — cutting is one-way, releasing cannot precede
    // cutting — so each has its own action rather than a field here.
    // Arrange / Act
    render(
      <EditVersionForm
        version={version}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(screen.queryByText('Cut Date')).not.toBeInTheDocument()
    expect(screen.queryByText('Released Date')).not.toBeInTheDocument()
  })

  it('keeps a sequence it cannot edit', async () => {
    // The update is a whole-record overwrite, so a field this form omits is cleared rather than left
    // alone. Sequence has no input here, so it has to be carried through explicitly or saving a
    // rename would silently drop an ordering an import had set.
    // Arrange
    render(
      <EditVersionForm
        version={version}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(updateVersion).toHaveBeenCalledWith(
      expect.objectContaining({
        request: expect.objectContaining({ sequence: 30 }),
      }),
    )
  })
})
