'use client'

import {
  RecordStatusHistory,
  type StatusHistoryView,
} from '@/src/components/common/status-workflows'
import { StatusTransitionDto } from '@/src/services/wayd-api'
import { useGetReleaseStatusWorkflowQuery } from '@/src/store/features/product-management/releases-api'
import { useState } from 'react'

export interface ReleaseStatusHistoryProps {
  /** The key the page was routed with, which the history is fetched and tagged by too. */
  idOrKey: string
  transitions: StatusTransitionDto[] | undefined
  isLoading: boolean
}

/** A release's status history, as a timeline or drawn on its workflow. */
const ReleaseStatusHistory = ({
  idOrKey,
  transitions,
  isLoading,
}: ReleaseStatusHistoryProps) => {
  const [view, setView] = useState<StatusHistoryView>('timeline')
  const workflow = useGetReleaseStatusWorkflowQuery(idOrKey, {
    skip: view !== 'diagram',
  })

  return (
    <RecordStatusHistory
      transitions={transitions}
      isLoading={isLoading}
      emptyDescription="No status changes have been recorded for this release."
      view={view}
      onViewChange={setView}
      workflow={workflow.data}
      isWorkflowLoading={workflow.isLoading}
      isWorkflowError={workflow.isError}
    />
  )
}

export default ReleaseStatusHistory
