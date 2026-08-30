'use client'

import { WaydGrid, createCsvColumn } from '@/src/components/common/wayd-grid'
import { ProductDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
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
    // Linked by key rather than id, so the URL carries something a reader recognises.
    cell: ({ row }) => (
      <Link href={`/product-management/products/${row.original.key}`}>
        {row.original.name}
      </Link>
    ),
  },
  {
    id: 'productType',
    accessorKey: 'productType.name',
    header: 'Type',
    size: 140,
    meta: { filterType: 'set' },
  },
  {
    id: 'status',
    accessorKey: 'status.name',
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
          accessorKey: 'parent.name',
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
    getValues: (row) => (row.tags ?? []).map((t) => t.tagName).sort(),
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
