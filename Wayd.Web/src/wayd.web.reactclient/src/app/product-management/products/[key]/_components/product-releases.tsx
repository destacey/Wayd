'use client'

import { ReleasesGrid } from '@/src/app/product-management/releases/_components'
import { ReleaseDto } from '@/src/services/wayd-api'

export interface ProductReleasesProps {
  releases: ReleaseDto[]
  isLoading: boolean
  refetch: () => void
}

/**
 * The releases announced under one product.
 *
 * The product column is hidden: every row here shares it, so repeating it costs width and says
 * nothing.
 *
 * A release announcing work across product lines carries no product and is deliberately absent, since
 * the filter this list is built from excludes it — belonging to no single product, listing it under
 * one would misstate what that product announced.
 */
const ProductReleases = ({
  releases,
  isLoading,
  refetch,
}: ProductReleasesProps) => (
  <ReleasesGrid
    releases={releases}
    isLoading={isLoading}
    refetch={refetch}
    showProduct={false}
    persistStateKey="product-management-product-releases"
    emptyMessage="No releases have been announced under this product. A release spanning product lines is not listed here."
  />
)

export default ProductReleases
