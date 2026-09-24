'use client'

import {
  revealOverflow,
  toggleExpansion,
  useDependencyMapExpansions,
  type DependencyEdgeStyle,
  type DependencyFarSide,
  type DependencyMapExpansionState,
} from '@/src/components/common/dependency-map'
import {
  DependencyStrength,
  ProductDependenciesDto,
  ProductDto,
} from '@/src/services/wayd-api'
import { toFileName } from '@/src/utils'
import { Button, Card, Col, Segmented, Skeleton, theme, Typography } from 'antd'
import dynamic from 'next/dynamic'
import {
  buildProductDependencyNeighbourhood,
  useDependencyStrengthFilter,
  useExpandedProductDependencies,
  type DependencyStrengthFilter,
  type ProductDependencyExpansions,
} from '../../../_components/dependency-map'

// Loaded on demand: the graph canvas is the heaviest thing on this page and most products have no
// dependencies at all, so it must not sit in the bundle every product page pays for.
const DependencyMap = dynamic(
  () => import('@/src/components/common/dependency-map/dependency-map'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 4 }} /> },
)

const { Text } = Typography

const FAR_SIDES: DependencyFarSide[] = ['left', 'right']

// What relies on the product sits on its left, what it relies on on its right.
const EXPAND_TOOLTIPS: Record<DependencyFarSide, (label: string) => string> = {
  left: (label) => `Show what relies on ${label}`,
  right: (label) => `Show what ${label} relies on`,
}

export interface ProductDependencyMapCardProps {
  product: ProductDto
  /** This product's dependencies, already loaded for the Dependencies section. */
  dependencies?: ProductDependenciesDto
  /** Opens the Dependencies section, which the card links to. */
  onViewAll?: () => void
}

/**
 * The Overview's map of what a product relies on and what relies on it, with whatever the reader
 * expanded outward from it.
 *
 * Absent rather than empty when the product has no dependencies: a map of one node says less than no
 * map, and most products have none at all.
 */
const ProductDependencyMapCard = ({
  product,
  dependencies,
  onViewAll,
}: ProductDependencyMapCardProps) => {
  const { token } = theme.useToken()
  const [strengthFilter, setStrengthFilter] = useDependencyStrengthFilter()
  const [state, setState] = useDependencyMapExpansions(product.id)
  const hardOnly = strengthFilter === 'hard'

  const expandedIds = FAR_SIDES.flatMap((side) => Object.keys(state[side]))
  const loaded = useExpandedProductDependencies(expandedIds)

  const expansionsFor = (
    from: DependencyMapExpansionState,
  ): ProductDependencyExpansions => {
    const bySide = (side: DependencyFarSide) =>
      Object.fromEntries(
        Object.entries(from[side]).map(([id, options]) => [
          id,
          { ...loaded[id], showAll: options.showAll },
        ]),
      )

    return { left: bySide('left'), right: bySide('right') }
  }

  const options = {
    productId: product.id,
    productName: product.name,
    productKey: product.key,
    dependencies,
  }

  const build = (from: DependencyMapExpansionState) =>
    buildProductDependencyNeighbourhood({
      ...options,
      strengthFilter,
      expansions: expansionsFor(from),
      subjectShowAll: from.subjectShowAll,
    })

  // Whether the card shows is decided on every link, not the filtered ones: a product whose links are
  // all soft would otherwise lose the map, and with it the control that would turn the filter back off.
  const hasDependencies =
    buildProductDependencyNeighbourhood(options).edges.length > 0
  const neighbourhood = build(state)

  if (!hasDependencies) return null

  // Pruning waits while anything is loading: a product not yet reached is not one no longer reachable.
  const stillLoading = expandedIds.some(
    (id) => !loaded[id]?.dependencies && !loaded[id]?.isError,
  )

  const onToggleExpansion = (side: DependencyFarSide, productId: string) =>
    setState(
      toggleExpansion(
        state,
        side,
        productId,
        stillLoading ? null : (next) => build(next).placed,
      ),
    )

  const onShowAll = (side: DependencyFarSide, ownerId: string | null) =>
    setState(revealOverflow(state, side, ownerId))

  // Hard is drawn heavier and solid, soft lighter and dashed, so the two still read apart in a greyscale
  // print of the exported image.
  const edgeStyles: Record<string, DependencyEdgeStyle> = {
    [DependencyStrength.Hard]: { stroke: token.colorTextSecondary, width: 2 },
    [DependencyStrength.Soft]: {
      stroke: token.colorTextQuaternary,
      width: 1.5,
      dashed: true,
    },
  }

  // A full-width row of its own on the Overview's grid, which the card owns so that a product with no
  // dependencies leaves no empty column behind.
  return (
    <Col span={24}>
      <Card
        size="small"
        title="Dependencies"
        extra={
          onViewAll && (
            <Button
              type="link"
              size="small"
              onClick={onViewAll}
              styles={{ root: { padding: 0 } }}
            >
              View all
            </Button>
          )
        }
      >
        <DependencyMap
          nodes={neighbourhood.nodes}
          edges={neighbourhood.edges}
          edgeStyles={edgeStyles}
          expandTooltips={EXPAND_TOOLTIPS}
          height={
            neighbourhood.edges.length > 0 ? neighbourhood.height : undefined
          }
          // "map" rather than "dependencies": the grid exports the same links as CSV, and the two
          // files would otherwise be told apart only by their extension.
          fileStem={`${toFileName(product.name)}-dependency-map`}
          filters={
            <Segmented<DependencyStrengthFilter>
              size="small"
              aria-label="Dependency strength"
              value={strengthFilter}
              onChange={setStrengthFilter}
              options={[
                { label: 'All', value: 'all' },
                { label: 'Hard only', value: 'hard' },
              ]}
            />
          }
          emptyText="No hard dependencies in either direction."
          onToggleExpansion={onToggleExpansion}
          onShowAll={onShowAll}
        />
        <Text type="secondary">
          What this product relies on, and what relies on it.
          {hardOnly
            ? ' Showing hard dependencies only: what stops working if a product goes down.'
            : ' A solid line is a hard dependency, a dashed line a soft one.'}
          {neighbourhood.hasContainedRecords &&
            ' The box holds the products beneath this one that the dependencies were recorded against.'}
          {neighbourhood.edges.length > 0 &&
            ' Use + on a product to follow its own dependencies further out.'}
        </Text>
      </Card>
    </Col>
  )
}

export default ProductDependencyMapCard
