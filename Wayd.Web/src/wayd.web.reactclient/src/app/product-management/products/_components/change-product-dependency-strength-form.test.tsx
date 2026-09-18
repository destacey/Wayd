import { act, render, screen } from '@testing-library/react'
import {
  DependencyStrength,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import ChangeProductDependencyStrengthForm from './change-product-dependency-strength-form'

jest.unmock('dayjs')

const changeStrength = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useChangeProductDependencyStrengthMutation: () => [changeStrength],
}))

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined
let formInstance:
  | {
      setFieldsValue: (values: Record<string, unknown>) => void
      getFieldValue: (name: string) => unknown
    }
  | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render. See
// complete-deployment-form.test.tsx for why the OK button's enabled state is not asserted.
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

const dependency = {
  id: 'dependency-1',
  product: { id: 'web', key: 2, name: 'Storefront Web' },
  dependsOnProduct: { id: 'identity', key: 3, name: 'Identity Service' },
  strength: DependencyStrength.Soft,
  startsOn: new Date('2026-03-01T00:00:00Z'),
} as ProductDependencyDto

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
  changeStrength.mockResolvedValue({ data: 'new-id' })
})

describe('ChangeProductDependencyStrengthForm', () => {
  it('starts on the other strength, since the current one changes nothing', () => {
    // Arrange / Act
    render(
      <ChangeProductDependencyStrengthForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(formInstance!.getFieldValue('strength')).toBe(
      DependencyStrength.Hard,
    )
  })

  it('changes the link on the product that has it, naming the product depended on', async () => {
    // Arrange
    render(
      <ChangeProductDependencyStrengthForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act
    await submit()

    // Assert
    expect(changeStrength).toHaveBeenCalledWith(
      expect.objectContaining({
        productId: 'web',
        dependencyId: 'dependency-1',
        dependsOnProductId: 'identity',
      }),
    )
  })

  it('explains that a dependency started today cannot change strength until tomorrow', () => {
    // Arrange — the current dependency would have to end the day before it started
    const startedToday = { ...dependency, startsOn: new Date() }

    // Act
    render(
      <ChangeProductDependencyStrengthForm
        dependency={startedToday}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(
      screen.getByText('This dependency started today'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Change' })).toBeDisabled()
  })

  it('does not warn about a dependency that started before today', () => {
    // Arrange / Act
    render(
      <ChangeProductDependencyStrengthForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(
      screen.queryByText('This dependency started today'),
    ).not.toBeInTheDocument()
  })
})
