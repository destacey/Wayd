import { act, fireEvent, render, screen } from '@testing-library/react'
import dayjs from 'dayjs'
import CreatePersonalAccessTokenForm from './create-personal-access-token-form'

// jest.setup stubs dayjs down to formatting; the form's default expiry and DatePicker need the real module.
jest.unmock('dayjs')

const createToken = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock(
  '@/src/store/features/user-management/personal-access-tokens-api',
  () => ({
    useCreatePersonalAccessTokenMutation: () => [createToken],
  }),
)

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined
let formInstance:
  | {
      setFieldsValue: (values: Record<string, unknown>) => void
      getFieldValue: (name: string) => unknown
    }
  | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render.
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
      formInstance = form
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
    <CreatePersonalAccessTokenForm
      onFormCreate={() => {}}
      onFormCancel={() => {}}
    />,
  )

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
  createToken.mockResolvedValue({ data: { token: 'plain-token' } })
})

describe('CreatePersonalAccessTokenForm', () => {
  it('starts on full access', () => {
    // Act
    renderForm()

    // Assert
    expect(screen.getByRole('radio', { name: /Full access/ })).toBeChecked()
    expect(formInstance!.getFieldValue('scope')).toBe('Write')
  })

  it('creates a read-only token when read-only access is chosen', async () => {
    // Arrange
    renderForm()
    act(() =>
      formInstance!.setFieldsValue({
        name: 'Reports',
        expiresAt: dayjs('2027-01-01T00:00:00Z'),
      }),
    )
    fireEvent.click(screen.getByRole('radio', { name: /Read-only access/ }))

    // Act
    await submit()

    // Assert
    expect(createToken).toHaveBeenCalledWith({
      name: 'Reports',
      expiresAt: dayjs('2027-01-01T00:00:00Z').toDate(),
      scope: 'Read',
    })
  })
})
