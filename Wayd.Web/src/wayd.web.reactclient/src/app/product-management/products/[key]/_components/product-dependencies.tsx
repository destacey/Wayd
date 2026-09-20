'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  renderProductLink,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import {
  ProductDependenciesDto,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { Segmented, Space, Switch, Typography } from 'antd'
import { useState } from 'react'
import { DependencyStrengthTag } from '../../_components/dependency-strength'
import { InteractionStyleTags } from '../../_components/interaction-style'
import useProductDependencyActions from './use-product-dependency-actions'

const { Text } = Typography

export type DependencyDirection = 'dependsOn' | 'usedBy'

export interface ProductDependenciesProps {
  productId: string
  dependencies: ProductDependenciesDto | undefined
  isLoading: boolean
  refetch: () => void
  includeEnded: boolean
  onIncludeEndedChange: (includeEnded: boolean) => void
}

/**
 * The columns for one direction.
 *
 * The far end leads, because it is what the list is about. The near end — which part of this product the
 * dependency starts from, or lands on — is shown only where it is not the product itself, so a product with no
 * children reads as a plain list and a platform's rolled-up rows say which service each belongs to.
 */
export const buildDependencyColumns = (
  direction: DependencyDirection,
  productId: string,
  getActionItems: ReturnType<
    typeof useProductDependencyActions
  >['getActionItems'],
): ColumnDef<ProductDependencyDto, any>[] => {
  const farEnd = (row: ProductDependencyDto) =>
    direction === 'dependsOn' ? row.dependsOnProduct : row.product
  const nearEnd = (row: ProductDependencyDto) =>
    direction === 'dependsOn' ? row.product : row.dependsOnProduct

  return [
    createActionsColumn<ProductDependencyDto>({
      getItems: getActionItems,
      ariaLabel: 'Dependency actions',
    }),
    {
      id: 'farEnd',
      accessorFn: (row) => farEnd(row).name,
      header: direction === 'dependsOn' ? 'Depends On' : 'Used By',
      size: 220,
      meta: { filterType: 'set' },
      cell: ({ row }) => renderProductLink(farEnd(row.original)),
    },
    {
      id: 'strength',
      accessorKey: 'strength',
      header: 'Strength',
      size: 110,
      meta: { filterType: 'set' },
      cell: ({ row }) => (
        <DependencyStrengthTag strength={row.original.strength} />
      ),
    },
    {
      id: 'interaction',
      // Sorts and filters on the recorded styles in one string, so "Synchronous" and "Synchronous,
      // Asynchronous" are distinct values rather than a set the filter would flatten. An unrecorded row
      // sorts and filters as "Not recorded" for the same reason the cell says so: blank reads as "none".
      accessorFn: (row) =>
        row.interactionStyles?.length
          ? row.interactionStyles.join(', ')
          : 'Not recorded',
      header: 'Interaction',
      size: 170,
      meta: { filterType: 'set' },
      cell: ({ row }) => (
        <InteractionStyleTags styles={row.original.interactionStyles} />
      ),
    },
    {
      id: 'nearEnd',
      accessorFn: (row) =>
        nearEnd(row).id === productId ? '' : nearEnd(row).name,
      header: direction === 'dependsOn' ? 'From' : 'Via',
      size: 200,
      meta: { filterType: 'set' },
      cell: ({ row }) =>
        nearEnd(row.original).id === productId
          ? null
          : renderProductLink(nearEnd(row.original)),
    },
    {
      id: 'description',
      accessorKey: 'description',
      header: 'Description',
      size: 320,
    },
    {
      id: 'startsOn',
      accessorKey: 'startsOn',
      header: 'Started',
      size: 170,
      meta: { columnType: 'dateOnly' },
    },
    {
      id: 'endsOn',
      accessorKey: 'endsOn',
      header: 'Last Day',
      size: 170,
      meta: { columnType: 'dateOnly' },
    },
  ]
}

/**
 * What a product depends on and what depends on it, one direction at a time.
 *
 * One grid with a switch between the directions rather than two stacked grids: each grid sizes itself to the
 * space below it, so a second one would get none.
 *
 * Both lists already include links from everything beneath the product, so a platform shows what its services
 * depend on without anyone recording it twice.
 */
const ProductDependencies = ({
  productId,
  dependencies,
  isLoading,
  refetch,
  includeEnded,
  onIncludeEndedChange,
}: ProductDependenciesProps) => {
  const [direction, setDirection] = useState<DependencyDirection>('dependsOn')
  const { getActionItems, dialogs } = useProductDependencyActions()

  const dependsOn = dependencies?.dependsOn ?? []
  const usedBy = dependencies?.usedBy ?? []

  return (
    <>
      <WaydGrid
        columns={buildDependencyColumns(direction, productId, getActionItems)}
        data={direction === 'dependsOn' ? dependsOn : usedBy}
        isLoading={isLoading}
        onRefresh={refetch}
        leftSlot={
          <Segmented<DependencyDirection>
            value={direction}
            onChange={setDirection}
            options={[
              { value: 'dependsOn', label: `Depends On (${dependsOn.length})` },
              { value: 'usedBy', label: `Used By (${usedBy.length})` },
            ]}
          />
        }
        rightSlot={
          <Space>
            <Switch
              id="include-ended-dependencies"
              size="small"
              checked={includeEnded}
              onChange={onIncludeEndedChange}
            />
            <Text type="secondary">Show ended</Text>
          </Space>
        }
        csvFileName={
          direction === 'dependsOn' ? 'product-depends-on' : 'product-used-by'
        }
        persistStateKey={`product-management-product-dependencies-${direction}`}
        emptyMessage={
          direction === 'dependsOn'
            ? 'This product has no dependencies recorded.'
            : 'No other product has a dependency on this product recorded.'
        }
      />

      {dialogs}
    </>
  )
}

export default ProductDependencies
