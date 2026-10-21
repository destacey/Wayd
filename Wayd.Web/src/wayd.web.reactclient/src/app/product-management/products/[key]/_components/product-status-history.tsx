'use client'

import {
  RecordStatusHistory,
  type StatusHistoryView,
} from '@/src/components/common/status-workflows'
import {
  useGetProductStatusHistoryQuery,
  useGetProductStatusWorkflowQuery,
} from '@/src/store/features/product-management/products-api'
import { useState } from 'react'

export interface ProductStatusHistoryProps {
  productId: string
}

/**
 * A product's status history, as a timeline or drawn on its workflow.
 *
 * Fetches by id rather than by the key the page was routed with: the query is tagged by its
 * argument, and a status mutation knows only the id — a key-tagged entry would go stale.
 */
const ProductStatusHistory = ({ productId }: ProductStatusHistoryProps) => {
  const [view, setView] = useState<StatusHistoryView>('timeline')
  const { data: transitions, isLoading } =
    useGetProductStatusHistoryQuery(productId)
  const workflow = useGetProductStatusWorkflowQuery(productId, {
    skip: view !== 'diagram',
  })

  return (
    <RecordStatusHistory
      transitions={transitions}
      isLoading={isLoading}
      emptyDescription="No status changes have been recorded for this product."
      view={view}
      onViewChange={setView}
      workflow={workflow.data}
      isWorkflowLoading={workflow.isLoading}
      isWorkflowError={workflow.isError}
    />
  )
}

export default ProductStatusHistory
