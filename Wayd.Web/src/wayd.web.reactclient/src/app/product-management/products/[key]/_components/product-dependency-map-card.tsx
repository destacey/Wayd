'use client'

import { ProductDependenciesDto, ProductDto } from '@/src/services/wayd-api'
import { toFileName } from '@/src/utils'
import { Card, Col, Segmented, Skeleton, Typography } from 'antd'
import dynamic from 'next/dynamic'
import {
  buildDependencyNeighbourhood,
  useDependencyMapExpansions,
  useDependencyStrengthFilter,
  useExpandedProductDependencies,
  type DependencyExpansions,
  type DependencyFarSide,
  type DependencyMapExpansionState,
  type DependencyStrengthFilter,
} from '../../../_components/dependency-map'

// Loaded on demand: the graph canvas is the heaviest thing on this page and most products have no
// dependencies at all, so it must not sit in the bundle every product page pays for.
const DependencyMap = dynamic(
  () => import('../../../_components/dependency-map/dependency-map'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 4 }} /> },
)

const { Text } = Typography

const FAR_SIDES: DependencyFarSide[] = ['usedBy', 'dependsOn']

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
  const [strengthFilter, setStrengthFilter] = useDependencyStrengthFilter()
  const [state, setState] = useDependencyMapExpansions(product.id)
  const hardOnly = strengthFilter === 'hard'

  const expandedIds = FAR_SIDES.flatMap((side) => Object.keys(state[side]))
  const loaded = useExpandedProductDependencies(expandedIds)

  const expansionsFor = (
    from: DependencyMapExpansionState,
  ): DependencyExpansions => {
    const bySide = (side: DependencyFarSide) =>
      Object.fromEntries(
        Object.entries(from[side]).map(([id, options]) => [
          id,
          { ...loaded[id], showAll: options.showAll },
        ]),
      )

    return { usedBy: bySide('usedBy'), dependsOn: bySide('dependsOn') }
  }

  const options = {
    productId: product.id,
    productName: product.name,
    productKey: product.key,
    dependencies,
  }

  const build = (from: DependencyMapExpansionState) =>
    buildDependencyNeighbourhood({
      ...options,
      strengthFilter,
      expansions: expansionsFor(from),
      subjectShowAll: from.subjectShowAll,
    })

  // Whether the card shows is decided on every link, not the filtered ones: a product whose links are
  // all soft would otherwise lose the map, and with it the control that would turn the filter back off.
  const hasDependencies = buildDependencyNeighbourhood(options).edges.length > 0
  const neighbourhood = build(state)

  if (!hasDependencies) return null

  const toggleExpansion = (side: DependencyFarSide, productId: string) => {
    const next: DependencyMapExpansionState = {
      usedBy: { ...state.usedBy },
      dependsOn: { ...state.dependsOn },
      subjectShowAll: state.subjectShowAll,
    }

    if (!next[side][productId]) {
      next[side][productId] = {}
      setState(next)
      return
    }

    delete next[side][productId]

    // Collapsing takes everything the expansion brought in with it, nested expansions included, so
    // expanding the product again starts from one column rather than restoring a stale path. Skipped
    // while anything is still loading, because a product not yet reached is not the same as one no
    // longer reachable.
    const stillLoading = expandedIds.some(
      (id) => !loaded[id]?.dependencies && !loaded[id]?.isError,
    )
    if (!stillLoading) {
      const { placed } = build(next)
      for (const s of FAR_SIDES) {
        for (const id of Object.keys(next[s])) {
          if (!placed[s].includes(id)) delete next[s][id]
        }
      }
    }

    setState(next)
  }

  const showAll = (side: DependencyFarSide, ownerId: string | null) => {
    if (ownerId === null) {
      setState({
        ...state,
        subjectShowAll: [...new Set([...state.subjectShowAll, side])],
      })
      return
    }

    setState({
      ...state,
      [side]: { ...state[side], [ownerId]: { showAll: true } },
    })
  }

  // A full-width row of its own on the Overview's grid, which the card owns so that a product with no
  // dependencies leaves no empty column behind.
  return (
    <Col span={24}>
      <Card
        size="small"
        title="Dependencies"
        extra={onViewAll && <a onClick={onViewAll}>View all</a>}
      >
        <DependencyMap
          nodes={neighbourhood.nodes}
          edges={neighbourhood.edges}
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
          onToggleExpansion={toggleExpansion}
          onShowAll={showAll}
        />
        <Text type="secondary">
          What this product relies on, and what relies on it.
          {hardOnly
            ? ' Showing hard dependencies only: what stops working if a product goes down.'
            : ' A solid line is a hard dependency, a dashed line a soft one.'}
          {neighbourhood.hasContainedProducts &&
            ' The box holds the products beneath this one that the dependencies were recorded against.'}
          {neighbourhood.edges.length > 0 &&
            ' Use + on a product to follow its own dependencies further out.'}
        </Text>
      </Card>
    </Col>
  )
}

export default ProductDependencyMapCard
