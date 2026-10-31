'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  renderProductLink,
  renderTeamLink,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import { ProductTeamLinkDto } from '@/src/services/wayd-api'
import useProductTeamLinkActions from './use-product-team-link-actions'

/**
 * Which side the grid is read from. On a product it lists teams; on a team it lists products.
 */
export type ProductTeamLinksView =
  { kind: 'product'; productId: string } | { kind: 'team' }

export interface ProductTeamLinksGridProps {
  view: ProductTeamLinksView
  links: ProductTeamLinkDto[] | undefined
  isLoading: boolean
  refetch: () => void
}

const pathOf = (link: ProductTeamLinkDto) =>
  link.productPath.map((p) => p.name).join(' › ')

/**
 * The columns for one side. The far side leads, because it is what the list is about.
 *
 * On a product, the product a row is on is shown only where it is not the page's product, so a product with no
 * children reads as a plain list and a product line's rolled-up rows say which product each belongs to. On a
 * team, each product's place in the tree is shown, since a bare "Checkout" does not say whose it is.
 */
export const buildProductTeamLinkColumns = (
  view: ProductTeamLinksView,
  getActionItems: ReturnType<
    typeof useProductTeamLinkActions
  >['getActionItems'],
): ColumnDef<ProductTeamLinkDto, any>[] => [
  createActionsColumn<ProductTeamLinkDto>({
    getItems: getActionItems,
    ariaLabel: 'Team link actions',
  }),
  ...(view.kind === 'product'
    ? [
        {
          id: 'team',
          accessorFn: (row: ProductTeamLinkDto) => row.team.name,
          header: 'Team',
          size: 220,
          meta: { filterType: 'set' },
          cell: ({ row }: { row: { original: ProductTeamLinkDto } }) =>
            renderTeamLink(row.original.team),
        } as ColumnDef<ProductTeamLinkDto, any>,
      ]
    : [
        {
          id: 'product',
          accessorFn: (row: ProductTeamLinkDto) => row.product.name,
          header: 'Product',
          size: 220,
          meta: { filterType: 'set' },
          cell: ({ row }: { row: { original: ProductTeamLinkDto } }) =>
            renderProductLink(row.original.product),
        } as ColumnDef<ProductTeamLinkDto, any>,
      ]),
  ...(view.kind === 'product'
    ? [
        {
          id: 'on',
          accessorFn: (row: ProductTeamLinkDto) =>
            row.product.id === view.productId ? '' : row.product.name,
          header: 'On',
          size: 200,
          meta: { filterType: 'set' },
          cell: ({ row }: { row: { original: ProductTeamLinkDto } }) =>
            row.original.product.id === view.productId
              ? null
              : renderProductLink(row.original.product),
        } as ColumnDef<ProductTeamLinkDto, any>,
      ]
    : [
        {
          id: 'path',
          accessorFn: pathOf,
          header: 'Within',
          size: 240,
        } as ColumnDef<ProductTeamLinkDto, any>,
      ]),
]

/**
 * Teams linked to products, read from either side: the teams on a product (rolled up from everything
 * beneath it) or the products a team is linked to.
 */
const ProductTeamLinksGrid = ({
  view,
  links,
  isLoading,
  refetch,
}: ProductTeamLinksGridProps) => {
  const { getActionItems, dialogs } = useProductTeamLinkActions()

  return (
    <>
      <WaydGrid
        columns={buildProductTeamLinkColumns(view, getActionItems)}
        data={links ?? []}
        getRowId={(link) => `${link.product.id}:${link.team.id}`}
        isLoading={isLoading}
        onRefresh={refetch}
        csvFileName={
          view.kind === 'product' ? 'product-teams' : 'team-products'
        }
        persistStateKey={`product-management-team-links-${view.kind}`}
        emptyMessage={
          view.kind === 'product'
            ? 'No team is linked to this product.'
            : 'This team is not linked to any product.'
        }
      />

      {dialogs}
    </>
  )
}

export default ProductTeamLinksGrid
