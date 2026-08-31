'use client'

import {
  WaydGrid,
  caseInsensitiveCompare,
  createCsvColumn,
} from '@/src/components/common/wayd-grid'
import { ProductDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import treeGridStyles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
import { CaretDownOutlined, CaretRightOutlined } from '@ant-design/icons'
import { Button, Flex } from 'antd'
import Link from 'next/link'
import { ReactElement } from 'react'
import { buildProductTree, ProductTreeNode } from './product-tree'

export interface ProductsGridProps {
  products: ProductDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactElement
  /**
   * Nests products under their parents. Default, because products are a hierarchy and a flat list
   * hides the thing that gives a component its meaning — what it is part of.
   */
  asTree?: boolean
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

/**
 * External Id is deliberately absent: nothing consumes it yet — it exists to reconcile hand-curated
 * nodes against a later automated feed — so it is column-chooser territory rather than a default.
 */
const buildColumns = <T extends ProductDto>(
  asTree: boolean,
): ColumnDef<T, any>[] => [
  { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Name',
    size: 280,
    meta: { filterEnableSet: true },
    // In tree mode the column draws its own indentation and expander: the grid renders rows flat and
    // leaves depth to the cell, so a plain link would put every level on the same line.
    // Linked by key rather than id, so the URL carries something a reader recognises.
    cell: ({ row }) => {
      const link = (
        <Link href={`/product-management/products/${row.original.key}`}>
          {row.original.name}
        </Link>
      )

      if (!asTree) return link

      return (
        <Flex align="center" gap={0} className={treeGridStyles.nameCell}>
          {Array.from({ length: row.depth }).map((_, index) => (
            <span key={index} className={treeGridStyles.indentSpacer} />
          ))}
          {row.getCanExpand() ? (
            <Button
              type="text"
              size="small"
              icon={
                row.getIsExpanded() ? <CaretDownOutlined /> : <CaretRightOutlined />
              }
              onClick={row.getToggleExpandedHandler()}
              className={treeGridStyles.expanderBtn}
            />
          ) : (
            <span className={treeGridStyles.indentSpacer} />
          )}
          {link}
        </Flex>
      )
    },
  },
  {
    id: 'type',
    accessorFn: (row) => row.type?.name ?? '',
    header: 'Type',
    size: 140,
    meta: { filterType: 'set' },
  },
  {
    id: 'status',
    accessorFn: (row) => row.status?.name ?? '',
    header: 'Status',
    size: 130,
    meta: { filterType: 'set' },
  },
  // The tree already shows the parent by position, so repeating it as a column is noise there.
  ...(asTree
    ? []
    : [
        {
          id: 'parent',
          accessorFn: (row: T) => row.parent?.name ?? '',
          header: 'Parent',
          size: 220,
          meta: { filterType: 'set' as const },
          cell: ({ row }: { row: { original: T } }) =>
            row.original.parent ? (
              <Link
                href={`/product-management/products/${row.original.parent.key}`}
              >
                {row.original.parent.name}
              </Link>
            ) : null,
        } as ColumnDef<T, any>,
      ]),
  {
    id: 'isReleasable',
    accessorKey: 'isReleasable',
    header: 'Releasable',
    size: 110,
    // Derived from the type, but shown on its own: whether releases can be cut against a product is
    // the most consequential thing about it, and reading it off the type name means knowing the
    // catalog by heart.
    meta: { columnType: 'yesNo' },
  },
  createCsvColumn<T>({
    id: 'tags',
    header: 'Tags',
    // Qualified by axis, as the header chips are: a bare "gold" does not say
    // whether it is a tier, a platform or a compliance scope. Sorting the
    // qualified string groups an axis's tags together, and the set filter is
    // built from these same values, so filtering picks one axis's tag rather
    // than every tag that happens to share a name.
    getValues: (row) =>
      (row.tags ?? [])
        .map((t) => `${t.categoryName} | ${t.tagName}`)
        .sort((a, b) => caseInsensitiveCompare(a, b)),
  }),
]

const ProductsGrid: React.FC<ProductsGridProps> = (props: ProductsGridProps) => {
  const { refetch, asTree = true } = props

  const refresh = async () => {
    refetch()
  }

  if (asTree) {
    return (
      <WaydGrid<ProductTreeNode>
        columns={buildColumns<ProductTreeNode>(true)}
        data={buildProductTree(props.products)}
        getSubRows={(row) => row.children}
        onRefresh={refresh}
        isLoading={props.isLoading}
        csvFileName="products"
        rightSlot={props.viewSelector}
        persistStateKey={props.persistStateKey}
        emptyMessage="No products found."
      />
    )
  }

  return (
    <WaydGrid<ProductDto>
      columns={buildColumns<ProductDto>(false)}
      data={props.products}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="products"
      rightSlot={props.viewSelector}
      persistStateKey={props.persistStateKey}
      emptyMessage="No products found."
    />
  )
}

export default ProductsGrid
