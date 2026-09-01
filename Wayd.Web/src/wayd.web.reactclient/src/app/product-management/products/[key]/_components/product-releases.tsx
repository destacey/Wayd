'use client'

import { ReleasesGrid } from '@/src/app/delivery/releases/_components'
import { useGetReleasesQuery } from '@/src/store/features/delivery/releases-api'

export interface ProductReleasesProps {
  productId: string
}

/**
 * The releases cut against one product.
 *
 * The product column is hidden: every row here shares it, so repeating it costs width and says
 * nothing.
 */
const ProductReleases = ({ productId }: ProductReleasesProps) => {
  const {
    data: releases,
    isLoading,
    refetch,
  } = useGetReleasesQuery({ productId })

  return (
    <ReleasesGrid
      releases={releases ?? []}
      isLoading={isLoading}
      refetch={refetch}
      showProduct={false}
      persistStateKey="product-management-product-releases"
    />
  )
}

export default ProductReleases
