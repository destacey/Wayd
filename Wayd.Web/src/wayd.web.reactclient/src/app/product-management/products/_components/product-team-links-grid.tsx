'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  renderProductLink,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import { ProductTeamLinkDto } from '@/src/services/wayd-api'
import useProductTeamLinkActions from './use-product-team-link-actions'

export interface ProductTeamLinksGridProps {
  links: ProductTeamLinkDto[] | undefined
  isLoading: boolean
  refetch: () => void
}

const pathOf = (link: ProductTeamLinkDto) =>
  link.productPath.map((p) => p.name).join(' › ')

/**
 * The columns of a team's products. Each product's place in the tree is shown, since a bare "Checkout" does not
 * say whose it is.
 */
export const buildProductTeamLinkColumns = (
  getActionItems: ReturnType<
    typeof useProductTeamLinkActions
  >['getActionItems'],
): ColumnDef<ProductTeamLinkDto, any>[] => [
  createActionsColumn<ProductTeamLinkDto>({
    getItems: getActionItems,
    ariaLabel: 'Team link actions',
  }),
  {
    id: 'product',
    accessorFn: (row) => row.product.name,
    header: 'Product',
    size: 220,
    meta: { filterType: 'set' },
    cell: ({ row }) => renderProductLink(row.original.product),
  },
  {
    id: 'path',
    accessorFn: pathOf,
    header: 'Within',
    size: 240,
  },
]

/**
 * The products a team or team of teams is linked to, for the team's page.
 */
const ProductTeamLinksGrid = ({
  links,
  isLoading,
  refetch,
}: ProductTeamLinksGridProps) => {
  const { getActionItems, dialogs } = useProductTeamLinkActions()

  return (
    <>
      <WaydGrid
        columns={buildProductTeamLinkColumns(getActionItems)}
        data={links ?? []}
        getRowId={(link) => `${link.product.id}:${link.team.id}`}
        isLoading={isLoading}
        onRefresh={refetch}
        csvFileName="team-products"
        persistStateKey="product-management-team-links-team"
        emptyMessage="This team is not linked to any product."
      />

      {dialogs}
    </>
  )
}

export default ProductTeamLinksGrid
