'use client'

import { VersionsGrid } from '@/src/app/delivery/versions/_components'
import { VersionDto } from '@/src/services/wayd-api'

export interface ProductVersionsProps {
  versions: VersionDto[]
  isLoading: boolean
  refetch: () => void
}

/**
 * The versions cut against one product.
 *
 * The product column is hidden: every row here shares it, so repeating it costs width and says
 * nothing.
 */
const ProductVersions = ({
  versions,
  isLoading,
  refetch,
}: ProductVersionsProps) => (
  <VersionsGrid
    versions={versions}
    isLoading={isLoading}
    refetch={refetch}
    showProduct={false}
    persistStateKey="product-management-product-versions"
  />
)

export default ProductVersions
