'use client'

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { ProductDto } from '@/src/services/wayd-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { TreeSelect } from 'antd'
import {
  buildMoveTargetTree,
  buildProductTree,
  ProductTreeNode,
} from './product-tree'

/**
 * Which nodes the caller may actually choose.
 *
 * `releasable` still shows the groupings above a releasable node — you cannot reach a service
 * without expanding the platform it sits under — but refuses them as the answer. A flat list could
 * simply omit them; a tree cannot, which is the whole reason this is a mode rather than a filter.
 *
 * It also drops any branch with nothing releasable anywhere beneath it. Such a branch can only ever
 * be expanded to reveal more of itself, and offering it implies a choice that leads somewhere.
 */
export type ProductSelectable = 'all' | 'releasable'

export interface ProductTreeSelectProps {
  /** Injected by Form.Item. */
  value?: string
  /** Injected by Form.Item. */
  onChange?: (value: string | undefined) => void
  /**
   * Which nodes may be chosen. `releasable` is required wherever the domain refuses a grouping —
   * cutting a version against one, for instance.
   */
  selectable?: ProductSelectable
  /**
   * Hides this node and everything beneath it.
   *
   * The branch is pruned rather than having its survivors hoisted: a grandchild shown at the root
   * would read as a legal target while its parent was hidden.
   */
  excludeSubtreeOf?: string
  placeholder?: string
  allowClear?: boolean
  disabled?: boolean
  id?: string
}

interface TreeSelectNode {
  value: string
  title: string
  selectable: boolean
  children: TreeSelectNode[]
}

/** Whether this node, or anything beneath it, can have versions cut against it. */
const leadsToReleasable = (node: ProductTreeNode): boolean =>
  node.isReleasable || node.children.some(leadsToReleasable)

/**
 * Titles are plain strings, not nodes.
 *
 * A ReactNode title renders, but antd then has no text to show in the closed selector or to search
 * against, so the chosen product comes back blank. The type suffix on an unselectable grouping is
 * safe to fold into the string: it can never be the selected value.
 */
const toTreeData = (
  nodes: ProductTreeNode[],
  selectable: ProductSelectable,
): TreeSelectNode[] =>
  nodes
    .filter((node) => selectable === 'all' || leadsToReleasable(node))
    .map((node) => {
      const isSelectable = selectable === 'all' || node.isReleasable

      return {
        value: node.id,
        // Named for what it is rather than greyed out: a grouping is a legitimate part of the path
        // to the node you want, and disabling it reads as "this branch is unavailable".
        title:
          isSelectable || !node.type
            ? node.name
            : `${node.name} · ${node.type.name}`,
        selectable: isSelectable,
        children: toTreeData(node.children, selectable),
      }
    })
    .sort((a, b) => caseInsensitiveCompare(a.title, b.title))

/**
 * Picks a product from the catalog tree.
 *
 * A tree rather than a flat list because the catalog nests, and the same name can mean different
 * things in different branches — "Gateway" under Payments is not the one under Trust &amp; Safety.
 * The hierarchy is the disambiguator, so hiding it makes the list ambiguous.
 */
const ProductTreeSelect = ({
  value,
  onChange,
  selectable = 'all',
  excludeSubtreeOf,
  placeholder = 'Select a product',
  allowClear = true,
  disabled,
  id,
}: ProductTreeSelectProps) => {
  const { data: products, isLoading } = useGetProductsQuery(undefined)

  const catalog = (products ?? []) as ProductDto[]
  const roots = excludeSubtreeOf
    ? buildMoveTargetTree(catalog, excludeSubtreeOf)
    : buildProductTree(catalog)
  const treeData = toTreeData(roots, selectable)

  return (
    <TreeSelect
      id={id}
      value={value}
      onChange={onChange}
      treeData={treeData}
      loading={isLoading}
      placeholder={placeholder}
      notFoundContent="No products found"
      disabled={disabled}
      treeLine
      treeDefaultExpandAll
      allowClear={allowClear}
      // Fills its container. Inside a Form.Item antd stretches it anyway, but standalone — a page
      // filter, say — it would otherwise collapse to the width of the placeholder.
      style={{ width: '100%' }}
      showSearch
      treeNodeFilterProp="title"
      // The popup sizes to its content rather than to the trigger. Each level of nesting indents,
      // so a popup constrained to the trigger's width leaves deep nodes a few characters wide —
      // names wrap onto two lines and then clip, which is worst for exactly the leaves a tree
      // exists to reach.
      popupMatchSelectWidth={false}
      styles={{
        popup: {
          root: {
            minWidth: 380,
            maxWidth: 640,
            maxHeight: 400,
            overflow: 'auto',
          },
        },
      }}
    />
  )
}

export default ProductTreeSelect
