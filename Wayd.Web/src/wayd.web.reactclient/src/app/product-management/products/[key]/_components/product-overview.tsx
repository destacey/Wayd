'use client'

import { MetricCard } from '@/src/components/common/metrics'
import {
  ProductDependenciesDto,
  ProductDto,
  VersionDto,
} from '@/src/services/wayd-api'
import { Col, Row } from 'antd'
import ProductDependencyMapCard from './product-dependency-map-card'
import { countReleasedWithin } from './version-cadence'

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

      {!dependenciesLoading && (
        <ProductDependencyMapCard
          product={product}
          dependencies={dependencies}
          onViewAll={
            dependenciesSectionId
              ? () => onNavigateToSection(dependenciesSectionId)
              : undefined
          }
        />
      )}
    </Row>
  )
}

export default ProductOverview
