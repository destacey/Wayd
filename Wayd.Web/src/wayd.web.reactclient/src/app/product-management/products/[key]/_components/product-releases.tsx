'use client'

import { ReleasesGrid } from '@/src/app/delivery/releases/_components'
import { ReleaseDto } from '@/src/services/wayd-api'

export interface ProductReleasesProps {
  releases: ReleaseDto[]
  isLoading: boolean
  refetch: () => void
}

/**
 * The releases cut against one product.
 *
 * The product column is hidden: every row here shares it, so repeating it costs width and says
 * nothing.
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
  />
)

export default ProductReleases
