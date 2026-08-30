'use client'

import { WaydGrid, createCsvColumn } from '@/src/components/common/wayd-grid'
import { ProductDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import Link from 'next/link'
import { ReactElement } from 'react'

export interface ProductsGridProps {
  products: ProductDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactElement
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const ProductsGrid: React.FC<ProductsGridProps> = (props: ProductsGridProps) => {
  const { refetch } = props

  const columns: ColumnDef<ProductDto, any>[] = [
    { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 240,
      meta: { filterEnableSet: true },
      // Linked by key rather than id, so the URL carries something a reader recognises.
      cell: ({ row }) => (
        <Link href={`/product-management/products/${row.original.key}`}>
          {row.original.name}
        </Link>
      ),
    },
    {
      id: 'productTypeName',
      accessorKey: 'productTypeName',
      header: 'Type',
      size: 140,
      meta: { filterType: 'set' },
    },
    {
      id: 'statusName',
      accessorKey: 'statusName',
      header: 'Status',
      size: 130,
      meta: { filterType: 'set' },
    },
    {
      id: 'parentName',
      accessorKey: 'parentName',
      header: 'Parent',
      size: 200,
      meta: { filterType: 'set' },
    },
    createCsvColumn<ProductDto>({
      id: 'tags',
      header: 'Tags',
      getValues: (row) => (row.tags ?? []).map((t) => t.tagName).sort(),
    }),
    {
      id: 'externalId',
      accessorKey: 'externalId',
      header: 'External Id',
      size: 180,
    },
  ]

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
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
