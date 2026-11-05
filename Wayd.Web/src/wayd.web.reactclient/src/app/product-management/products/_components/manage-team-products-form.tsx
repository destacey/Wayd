'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useConfirmModal } from '@/src/hooks'
import {
  useGetProductsQuery,
  useSetTeamProductsMutation,
} from '@/src/store/features/product-management/products-api'
import { toApiError } from '@/src/utils'
import { Flex, Input, Modal, Spin, Tree, Typography } from 'antd'
import type { DataNode } from 'antd/es/tree'
import { useState } from 'react'
import { buildProductTree, ProductTreeNode } from '../../_components'
import styles from './manage-team-products-form.module.css'

const { Text } = Typography

export interface ManageTeamProductsFormProps {
  teamId: string
  teamName: string
  /** The products the team is linked to now, which start checked. */
  currentProductIds: string[]
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * The catalog as a tree, keeping only the nodes whose name matches and the ancestors that lead to them, so a
 * match is still shown where it sits.
 */
export const toTreeData = (
  nodes: ProductTreeNode[],
  search: string,
): DataNode[] => {
  const term = search.trim().toLowerCase()

  return [...nodes]
    .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
    .flatMap((node): DataNode[] => {
      const children = toTreeData(node.children, search)
      const matches = term === '' || node.name.toLowerCase().includes(term)
      return matches || children.length > 0
        ? [{ key: node.id, title: node.name, children }]
        : []
    })
}

/**
 * Sets the products a team or team of teams is linked to, as a whole: checked products are linked, unchecked
 * ones unlinked. Parents and children are checked independently, since a link is on one product.
 */
const ManageTeamProductsForm = ({
  teamId,
  teamName,
  currentProductIds,
  onFormComplete,
  onFormCancel,
}: ManageTeamProductsFormProps) => {
  const messageApi = useMessage()
  const [checked, setChecked] = useState<string[]>(currentProductIds)
  const [search, setSearch] = useState('')

  const { data: products, isLoading } = useGetProductsQuery(undefined)
  const [setTeamProducts] = useSetTeamProductsMutation()

  const current = new Set(currentProductIds)
  const chosen = new Set(checked)
  const toLink = checked.filter((id) => !current.has(id)).length
  const toUnlink = currentProductIds.filter((id) => !chosen.has(id)).length
  const hasChanges = toLink > 0 || toUnlink > 0

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await setTeamProducts({
          teamId,
          productIds: checked,
        })
        if (response.error) throw response.error

        messageApi.success('Products updated.')
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            'An error occurred while updating the products. Please try again.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while updating the products. Please try again.',
    permission: 'Permissions.Products.Update',
  })

  // Toggles only the node clicked: with checkStrictly the tree's own list leaves out checked products the
  // search has hidden, which would unlink them.
  const toggle = (productId: string) =>
    setChecked((previous) =>
      previous.includes(productId)
        ? previous.filter((id) => id !== productId)
        : [...previous, productId],
    )

  const treeData = toTreeData(buildProductTree(products ?? []), search)

  return (
    <Modal
      title={`Manage Products — ${teamName}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !hasChanges }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Text type="secondary">
          Check the products this team works on. Link the most specific
          products: each product&apos;s parents show its teams too.
        </Text>
        <Input.Search
          allowClear
          placeholder="Search products"
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search products"
        />
        {isLoading ? (
          <Spin />
        ) : (
          <div className={styles.tree}>
            <Tree
              // Remounted per search so the filtered tree opens fully expanded.
              key={search}
              checkable
              checkStrictly
              selectable={false}
              defaultExpandAll
              treeData={treeData}
              checkedKeys={checked}
              onCheck={(_, info) => toggle(String(info.node.key))}
            />
          </div>
        )}
        <Text type="secondary" aria-live="polite">
          {hasChanges
            ? `${toLink} to link, ${toUnlink} to unlink`
            : 'No changes'}
        </Text>
      </Flex>
    </Modal>
  )
}

export default ManageTeamProductsForm
