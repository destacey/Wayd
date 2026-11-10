import { act, fireEvent, render, screen } from '@testing-library/react'
import dayjs from 'dayjs'
import CreatePersonalAccessTokenForm from './create-personal-access-token-form'
import type { FormInstance } from 'antd'
import type { UseModalFormOptions } from '@/src/hooks'

const createToken = vi.fn()

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock(
  '@/src/store/features/user-management/personal-access-tokens-api',
  () => ({
    useCreatePersonalAccessTokenMutation: () => [createToken],
  }),
)

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined
let formInstance: FormInstance | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
    useModalForm: <TValues,>({ onSubmit }: UseModalFormOptions<TValues>) => {
      const [form] = Form.useForm<TValues>()
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
        handleCancel: vi.fn(),
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
  vi.clearAllMocks()
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
