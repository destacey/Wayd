'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto, ReparentProductRequest } from '@/src/services/wayd-api'
import {
  useGetProductsQuery,
  useReparentProductMutation,
} from '@/src/store/features/product-management/products-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { buildMoveTargetTree, ProductTreeNode } from './product-tree'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, TreeSelect } from 'antd'

const { Item } = Form

export interface ReparentProductFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ReparentProductFormValues {
  parentId?: string
}

interface TreeSelectNode {
  value: string
  title: string
  children: TreeSelectNode[]
}

/**
 * Moves a product to a different parent, or to the root.
 *
 * The picker is a tree so the hierarchy being moved within is visible, and it
 * omits the product's own subtree: a product cannot become its own ancestor. The
 * API enforces that too, so this is about not offering the move rather than
 * about stopping it.
 */
const ReparentProductForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ReparentProductFormProps) => {
  const messageApi = useMessage()

  const [reparentProduct] = useReparentProductMutation()
  const { data: products, isLoading } = useGetProductsQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ReparentProductFormValues>({
      onSubmit: async (values: ReparentProductFormValues, form) => {
        try {
          const request = {
            id: product.id,
            parentId: values.parentId,
          } as ReparentProductRequest

          const response = await reparentProduct({ id: product.id, request })
          if (response.error) throw response.error

          messageApi.success('Product moved successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while moving the product. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while moving the product. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  const toTreeData = (nodes: ProductTreeNode[]): TreeSelectNode[] =>
    nodes
      .map((node) => ({
        value: node.id,
        title: node.name,
        children: toTreeData(node.children),
      }))
      .sort((a, b) => caseInsensitiveCompare(a.title, b.title))

  const treeData = toTreeData(buildMoveTargetTree(products ?? [], product.id))

  return (
    <Modal
      title="Move Product"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Move"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="reparent-product-form"
        initialValues={{ parentId: product.parent?.id }}
      >
        <Item
          name="parentId"
          label="Parent"
          extra="Clear this to make it a root product."
        >
          <TreeSelect
            treeData={treeData}
            loading={isLoading}
            placeholder="Select a parent"
            notFoundContent="No products found"
            treeLine
            treeDefaultExpandAll
            allowClear
            showSearch={{
              filterTreeNode: (input, node) =>
                node.title
                  ?.toString()
                  .toLowerCase()
                  .includes(input.toLowerCase()) ?? false,
            }}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ReparentProductForm
