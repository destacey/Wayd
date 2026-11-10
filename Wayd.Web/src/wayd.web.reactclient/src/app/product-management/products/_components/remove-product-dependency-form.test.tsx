import { act, render } from '@testing-library/react'
import {
  DependencyStrength,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import RemoveProductDependencyForm from './remove-product-dependency-form'
import type { FormInstance } from 'antd'
import type { UseModalFormOptions } from '@/src/hooks'

const removeDependency = vi.fn()

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock('@/src/store/features/product-management/products-api', () => ({
  useRemoveProductDependencyMutation: () => [removeDependency],
}))

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined
let formInstance: FormInstance | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render. See
// complete-deployment-form.test.tsx for why the OK button's enabled state is not asserted.
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

const dependency = {
  id: 'dependency-1',
  product: { id: 'web', key: 2, name: 'Storefront Web' },
  dependsOnProduct: { id: 'identity', key: 3, name: 'Identity Service' },
  strength: DependencyStrength.Soft,
  startsOn: '2026-03-01',
} as ProductDependencyDto

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  vi.clearAllMocks()
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
