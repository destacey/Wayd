'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import { MetricCard } from '@/src/components/common/metrics'
import { ProductDto } from '@/src/services/wayd-api'
import { Card, Col, Empty, Row } from 'antd'

export interface ProductOverviewProps {
  product: ProductDto
  /** This product's direct children, already loaded for the Products section. */
  childProducts: ProductDto[]
  childProductsLoading: boolean
  /** Navigates to a section by id, so section ids stay defined on the page. */
  onNavigateToSection: (sectionId: string) => void
  /** The id of the section listing child products, for the tile to link to. */
  productsSectionId: string
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
}: ProductOverviewProps) => {
  const releasableChildren = childProducts.filter((c) => c.isReleasable).length

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
      <Col xs={24} sm={12} md={8}>
        <MetricCard title="Tags" value={product.tags?.length ?? 0} />
      </Col>

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
