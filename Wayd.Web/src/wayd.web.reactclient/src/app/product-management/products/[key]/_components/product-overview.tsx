'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import { MetricCard } from '@/src/components/common/metrics'
import { ProductDto, ReleaseDto } from '@/src/services/wayd-api'
import { Card, Col, Empty, Row } from 'antd'
import { countReleasedWithin } from './release-cadence'

/**
 * How far back the release tile counts.
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
  /** This product's releases, already loaded for the Releases section. */
  releases?: ReleaseDto[]
  releasesLoading?: boolean
  /** The id of the section listing releases, for the tile to link to. */
  releasesSectionId?: string
}

/**
 * What a product is, at a glance.
 *
 * The child count comes from the same query the Products section uses, so the
 * tile cannot disagree with the list it summarises.
 */
const ProductOverview = ({
  product,
  childProducts,
  childProductsLoading,
  onNavigateToSection,
  productsSectionId,
  releases,
  releasesLoading,
  releasesSectionId,
}: ProductOverviewProps) => {
  const releasableChildren = childProducts.filter((c) => c.isReleasable).length

  const releasedInWindow = countReleasedWithin(releases, RELEASE_WINDOW_DAYS)

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
            secondaryValue={`${releases?.length ?? 0} total`}
            loading={releasesLoading}
            tooltip={`Released in the last ${RELEASE_WINDOW_DAYS} days. A release counts on the day it shipped, not when it was planned or cut.`}
            onClick={
              releasesSectionId
                ? () => onNavigateToSection(releasesSectionId)
                : undefined
            }
          />
        </Col>
      )}

      <Col span={24}>
        <Card size="small" title="Description">
          {product.description ? (
            <MarkdownRenderer markdown={product.description} />
          ) : (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description="No description."
            />
          )}
        </Card>
      </Col>
    </Row>
  )
}

export default ProductOverview
