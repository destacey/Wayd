import { act, render } from '@testing-library/react'
import { DependencyStrength, ProductDto } from '@/src/services/wayd-api'
import AddProductDependencyForm from './add-product-dependency-form'

jest.unmock('dayjs')

const addDependency = jest.fn()

let mockPickerProps: Record<string, unknown> = {}

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useAddProductDependencyMutation: () => [addDependency],
  useGetProductsQuery: () => ({
    data: [
      { id: 'trio', key: 1, name: 'Trio', tags: [] },
      {
        id: 'vms',
        key: 2,
        name: 'Trio VMS',
        parent: { id: 'trio', key: 1, name: 'Trio' },
        tags: [],
      },
    ],
  }),
}))

// The picker is exercised by its own suite; here what the form hands it is what matters.
jest.mock('../../_components', () => ({
  ...jest.requireActual('../../_components/product-tree'),
  ProductTreeSelect: (props: Record<string, unknown>) => {
    mockPickerProps = props
    return null
  },
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

const product = {
  id: 'vms',
  key: 2,
  name: 'Trio VMS',
  tags: [],
} as unknown as ProductDto

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
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
    expect(mockPickerProps.excludeSubtreeOf).toBe('vms')
    expect(mockPickerProps.unselectableIds).toEqual(['trio'])
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
    expect(productId).toBe('vms')
    expect(request.dependsOnProductId).toBe('identity')
    expect(request.strength).toBe(DependencyStrength.Hard)
    expect(request.startsOn).toBeUndefined()
  })
})
