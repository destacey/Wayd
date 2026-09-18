'use client'

import { MetricCard } from '@/src/components/common/metrics'
import {
  ProductDependenciesDto,
  ProductDto,
  VersionDto,
} from '@/src/services/wayd-api'
import { Card, Col, Row, Segmented, Skeleton, Typography } from 'antd'
import dynamic from 'next/dynamic'
import { toFileName } from '@/src/utils'
import {
  buildDependencyNeighbourhood,
  useDependencyStrengthFilter,
  type DependencyStrengthFilter,
} from '../../../_components/dependency-map'
import { countReleasedWithin } from './version-cadence'

// Loaded on demand: the graph canvas is the heaviest thing on this page and most products have no
// dependencies at all, so it must not sit in the bundle every product page pays for.
const DependencyMap = dynamic(
  () => import('../../../_components/dependency-map/dependency-map'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 4 }} /> },
)

const { Text } = Typography

/**
 * How far back the version tile counts.
 *
 * Ninety days rather than thirty: long enough to smooth the week-to-week noise a single product's
 * cadence shows, and it lines up with a quarter. DORA prescribes no window — it defines rate bands —
 * so this is a reporting choice rather than a standard.
 */
const RELEASE_WINDOW_DAYS = 90

export interface ProductOverviewProps {
  product: ProductDto
  /** This product's direct children, already loaded for the Products section. */
  childProducts: ProductDto[]
  childProductsLoading: boolean
  /** Navigates to a section by id, so section ids stay defined on the page. */
  onNavigateToSection: (sectionId: string) => void
  /** The id of the section listing child products, for the tile to link to. */
  productsSectionId: string
  /** This product's versions, already loaded for the Releases section. */
  versions?: VersionDto[]
  versionsLoading?: boolean
  /** The id of the section listing versions, for the tile to link to. */
  versionsSectionId?: string
  /** This product's dependencies, already loaded for the Dependencies section. */
  dependencies?: ProductDependenciesDto
  dependenciesLoading?: boolean
  /** The id of the Dependencies section, for the map's overflow link. */
  dependenciesSectionId?: string
}

/**
 * What a product is, at a glance.
 *
 * The child count comes from the same query the Products section uses, so the
 * tile cannot disagree with the list it summarises.
 *
 * No description here: the record's facts panel already renders it, and no other record repeats a fact
 * on its overview.
 */
const ProductOverview = ({
  product,
  childProducts,
  childProductsLoading,
  onNavigateToSection,
  productsSectionId,
  versions,
  versionsLoading,
  versionsSectionId,
  dependencies,
  dependenciesLoading,
  dependenciesSectionId,
}: ProductOverviewProps) => {
  const releasableChildren = childProducts.filter((c) => c.isReleasable).length

  const releasedInWindow = countReleasedWithin(versions, RELEASE_WINDOW_DAYS)

  const [strengthFilter, setStrengthFilter] = useDependencyStrengthFilter()
  const hardOnly = strengthFilter === 'hard'

  const mapOptions = {
    productId: product.id,
    productName: product.name,
    productKey: product.key,
    dependencies,
  }
  // Whether the card shows is decided on every link, not the filtered ones: a product whose links are
  // all soft would otherwise lose the map, and with it the control that would turn the filter back off.
  const hasDependencies =
    buildDependencyNeighbourhood(mapOptions).edges.length > 0
  const neighbourhood = buildDependencyNeighbourhood({
    ...mapOptions,
    strengthFilter,
  })

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} sm={12} md={8}>
        <MetricCard
          title="Products"
          value={childProducts.length}
          loading={childProductsLoading}
          onClick={() => onNavigateToSection(productsSectionId)}
        />
      </Col>
      <Col xs={24} sm={12} md={8}>
        <MetricCard
          title="Releasable Products"
          value={releasableChildren}
          loading={childProductsLoading}
        />
      </Col>
      {product.isReleasable && (
        <Col xs={24} sm={12} md={8}>
          <MetricCard
            title={`Releases (${RELEASE_WINDOW_DAYS}d)`}
            value={releasedInWindow}
            secondaryValue={`${versions?.length ?? 0} total`}
            loading={versionsLoading}
            tooltip={`Released in the last ${RELEASE_WINDOW_DAYS} days. A version counts on the day it shipped, not when it was planned or cut.`}
            onClick={
              versionsSectionId
                ? () => onNavigateToSection(versionsSectionId)
                : undefined
            }
          />
        </Col>
      )}

      {/* Absent rather than empty when nothing depends on this product: a map of one node says less
          than no map, and most products have no dependencies at all. */}
      {!dependenciesLoading && hasDependencies && (
        <Col span={24}>
          <Card
            size="small"
            title="Dependencies"
            extra={
              dependenciesSectionId && (
                <a onClick={() => onNavigateToSection(dependenciesSectionId)}>
                  {/* The grid is not filtered, so the count says which links it is counting. */}
                  {neighbourhood.hiddenCount > 0
                    ? `+${neighbourhood.hiddenCount} more${hardOnly ? ' hard' : ''}`
                    : 'View all'}
                </a>
              )
            }
          >
            <DependencyMap
              nodes={neighbourhood.nodes}
              edges={neighbourhood.edges}
              height={
                neighbourhood.edges.length > 0
                  ? neighbourhood.height
                  : undefined
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
            />
            <Text type="secondary">
              What this product relies on, and what relies on it.
              {hardOnly
                ? ' Showing hard dependencies only: what stops working if a product goes down.'
                : ' A solid line is a hard dependency, a dashed line a soft one.'}
              {neighbourhood.hasContainedProducts &&
                ' The box holds the products beneath this one that the dependencies were recorded against.'}
            </Text>
          </Card>
        </Col>
      )}
    </Row>
  )
}

export default ProductOverview
