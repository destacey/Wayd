import { act, render } from '@testing-library/react'
import { DependencyStrength, ProductDto } from '@/src/services/wayd-api'
import AddProductDependencyForm from './add-product-dependency-form'
import type { FormInstance } from 'antd'
import type { UseModalFormOptions } from '@/src/hooks'
import type { ProductTreeSelectProps } from '../../_components'

const addDependency = vi.fn()

let mockPickerProps: Partial<ProductTreeSelectProps> = {}

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock('@/src/store/features/product-management/products-api', () => ({
  useAddProductDependencyMutation: () => [addDependency],
  useGetProductsQuery: () => ({
    data: [
      { id: 'storefront', key: 1, name: 'Storefront', tags: [] },
      {
        id: 'web',
        key: 2,
        name: 'Storefront Web',
        parent: { id: 'storefront', key: 1, name: 'Storefront' },
        tags: [],
      },
    ],
  }),
}))

// The picker is exercised by its own suite; here what the form hands it is what matters.
vi.mock(import('../../_components'), async () => ({
  ...(await vi.importActual<typeof import('../../_components/product-tree')>(
    '../../_components/product-tree',
  )),
  ProductTreeSelect: (props: ProductTreeSelectProps) => {
    mockPickerProps = props
    return <></>
  },
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

const product = {
  id: 'web',
  key: 2,
  name: 'Storefront Web',
  tags: [],
} as unknown as ProductDto

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  vi.clearAllMocks()
  addDependency.mockResolvedValue({ data: 'new-id' })
})

describe('AddProductDependencyForm', () => {
  it('hides the product subtree and shows its ancestors without letting them be chosen', () => {
    // Arrange / Act
    render(
      <AddProductDependencyForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(mockPickerProps.excludeSubtreeOf).toBe('web')
    expect(mockPickerProps.unselectableIds).toEqual(['storefront'])
  })

  it('adds the dependency to the product, leaving the start to the server when none is given', async () => {
    // Arrange
    render(
      <AddProductDependencyForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )
    act(() =>
      formInstance!.setFieldsValue({
        dependsOnProductId: 'identity',
        strength: DependencyStrength.Hard,
      }),
    )

    // Act
    await submit()

    // Assert
    expect(addDependency).toHaveBeenCalledTimes(1)
    const { productId, request } = addDependency.mock.calls[0][0]
    expect(productId).toBe('web')
    expect(request.dependsOnProductId).toBe('identity')
    expect(request.strength).toBe(DependencyStrength.Hard)
    expect(request.startsOn).toBeUndefined()
  })
})
