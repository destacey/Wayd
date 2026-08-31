'use client'

import { StatusHistoryTimeline } from '@/src/components/common/status-workflows'
import { useGetProductStatusHistoryQuery } from '@/src/store/features/product-management/products-api'

export interface ProductStatusHistoryProps {
  productId: string
}

/**
 * A product's status history.
 *
 * Fetches by id rather than by the key the page was routed with: the query is tagged by its
 * argument, and a status mutation knows only the id — a key-tagged entry would go stale.
 */
const ProductStatusHistory = ({ productId }: ProductStatusHistoryProps) => {
  const { data: transitions, isLoading } =
    useGetProductStatusHistoryQuery(productId)

  return (
    <StatusHistoryTimeline
      transitions={transitions}
      isLoading={isLoading}
      emptyDescription="No status changes have been recorded for this product."
    />
  )
}

export default ProductStatusHistory
