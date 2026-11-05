import { act, fireEvent, render, screen } from '@testing-library/react'
import { OidcProviderType } from '@/src/services/wayd-api'
import CreateOidcProviderForm from './create-oidc-provider-form'

const createOidcProvider = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/user-management/oidc-providers-api', () => ({
  useCreateOidcProviderMutation: () => [createOidcProvider],
}))

jest.mock('@/src/store/features/user-management/roles-api', () => ({
  useGetRolesQuery: () => ({ data: [] }),
}))

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined

// Not spread from the real barrel: it re-exports store-bound hooks, and pulling those in
// initialises a store this form never touches. The form instance is real, though — antd's Form
// binds to it, and a stub breaks on render.
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
      const handleOk = async () => {
        await onSubmit(form.getFieldsValue(true), form)
      }
      submitForm = handleOk
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk,
        handleCancel: jest.fn(),
      }
    },
  }
})

const renderForm = () =>
  render(
    <CreateOidcProviderForm
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

const chooseProviderType = (label: string) => {
  fireEvent.mouseDown(screen.getByRole('combobox', { name: /^Provider Type/ }))
  fireEvent.click(screen.getByTitle(label))
}

const nameInput = () => screen.getByLabelText('Name') as HTMLInputElement

beforeEach(() => {
  jest.clearAllMocks()
  createOidcProvider.mockResolvedValue({ data: 'new-id' })
})

/**
 * The OK button's enabled state is not asserted: `useModalForm` derives it from
 * `form.validateFields()`, whose promise never settles under jsdom. `handleOk` is invoked
 * directly instead.
 */
describe('CreateOidcProviderForm', () => {
  it('asks for the provider type before the name', () => {
    // Arrange / Act
    renderForm()

    // Assert
    const labels = Array.from(
      document.querySelectorAll('.ant-form-item-label label'),
    ).map((label) => label.textContent)
    expect(labels.slice(0, 2)).toEqual(['Provider Type', 'Name'])
  })

  it('leaves the name editable and empty for a generic OIDC provider', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(nameInput()).toBeEnabled()
    expect(nameInput()).toHaveValue('')
  })

  it('fixes the name of a Microsoft Entra ID provider to MicrosoftEntraId', async () => {
    // Arrange — the API recognises an Entra provider only by this name.
    renderForm()

    // Act
    chooseProviderType('Microsoft Entra ID')

    // Assert
    expect(nameInput()).toHaveValue('MicrosoftEntraId')
    expect(nameInput()).toBeDisabled()

    await act(async () => {
      await submitForm!()
    })
    expect(createOidcProvider).toHaveBeenCalledTimes(1)
    expect(createOidcProvider.mock.calls[0][0]).toMatchObject({
      name: 'MicrosoftEntraId',
      providerType: OidcProviderType.MicrosoftEntraId,
    })
  })

  it('clears and unlocks the name when switching back to generic OIDC', () => {
    // Arrange
    renderForm()
    chooseProviderType('Microsoft Entra ID')

    // Act
    chooseProviderType('Generic OIDC')

    // Assert — the Entra name would fail the generic name rule, so it is not kept.
    expect(nameInput()).toBeEnabled()
    expect(nameInput()).toHaveValue('')
  })
})
