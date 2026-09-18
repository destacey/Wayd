import { act, render } from '@testing-library/react'
import {
  DependencyStrength,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import RemoveProductDependencyForm from './remove-product-dependency-form'

jest.unmock('dayjs')

const removeDependency = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useRemoveProductDependencyMutation: () => [removeDependency],
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
  removeDependency.mockResolvedValue({ data: undefined })
})

describe('RemoveProductDependencyForm', () => {
  it('sends the reason with the link it removes', async () => {
    // Arrange
    render(
      <RemoveProductDependencyForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )
    act(() =>
      formInstance!.setFieldsValue({
        reason: 'Recorded against the wrong product',
      }),
    )

    // Act
    await submit()

    // Assert
    expect(removeDependency).toHaveBeenCalledWith({
      productId: 'web',
      dependencyId: 'dependency-1',
      dependsOnProductId: 'identity',
      request: { reason: 'Recorded against the wrong product' },
    })
  })
})
