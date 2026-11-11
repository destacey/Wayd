import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { ProductDto } from '@/src/services/wayd-api'
import EditProductForm from './edit-product-form'
import type { UseModalFormOptions } from '@/src/hooks'

const product = {
  id: 'product-1',
  key: 7,
  name: 'Dispatch API',
  description: 'The dispatch surface.',
  externalId: 'acme/dispatch-api',
  tags: [],
} as unknown as ProductDto

const updateProduct = vi.fn().mockResolvedValue({ data: undefined })

vi.mock('@/src/components/common/markdown', () => ({
  MarkdownEditor: () => <textarea data-testid="markdown-editor" />,
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

// Not spread from the real barrel: it re-exports store-bound hooks, and pulling
// those in initialises a store this form never touches. The form instance is
// real, though — antd's Form binds to it, and a stub breaks on render.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
    useModalForm: <TValues,>({ onSubmit }: UseModalFormOptions<TValues>) => {
      const [form] = Form.useForm<TValues>()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          await onSubmit(form.getFieldsValue(), form)
        },
        handleCancel: vi.fn(),
      }
    },
  }
})

vi.mock('@/src/store/features/product-management/products-api', () => ({
  useUpdateProductMutation: () => [updateProduct],
}))

describe('EditProductForm', () => {
  beforeEach(() => updateProduct.mockClear())

  it('does not ask for the external link', () => {
    // Linking a product to its repository is a different intent from renaming
    // it, and has its own action.
    // Arrange / Act
    render(
      <EditProductForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(screen.queryByText('External Id')).not.toBeInTheDocument()
  })

  it('does not send the external link at all', async () => {
    // The link has its own endpoint. Sending it from here would put it back
    // under whole-record PUT semantics, where a rename clears it.
    // Arrange
    render(
      <EditProductForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(updateProduct).toHaveBeenCalled()
    expect(updateProduct.mock.calls[0][0].request).not.toHaveProperty(
      'externalId',
    )
  })

  it('disables the name field when antd disables its components', () => {
    // antd reaches a field through a React context, which only a component
    // imported from `antd` itself reads.
    // Arrange / Act
    render(
      <ConfigProvider componentDisabled>
        <EditProductForm
          product={product}
          onFormComplete={() => {}}
          onFormCancel={() => {}}
        />
      </ConfigProvider>,
    )

    // Assert
    expect(screen.getByRole('textbox', { name: 'Name' })).toBeDisabled()
  })
})
