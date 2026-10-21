'use client'

import {
  RecordStatusHistory,
  type StatusHistoryView,
} from '@/src/components/common/status-workflows'
import { StatusTransitionDto } from '@/src/services/wayd-api'
import { useGetReleasePackageStatusWorkflowQuery } from '@/src/store/features/product-management/release-packages-api'
import { useState } from 'react'

export interface ReleasePackageStatusHistoryProps {
  /** The key the page was routed with, which the history is fetched and tagged by too. */
  idOrKey: string
  transitions: StatusTransitionDto[] | undefined
  isLoading: boolean
}

/** A package's status history, as a timeline or drawn on its workflow. */
const ReleasePackageStatusHistory = ({
  idOrKey,
  transitions,
  isLoading,
}: ReleasePackageStatusHistoryProps) => {
  const [view, setView] = useState<StatusHistoryView>('timeline')
  const workflow = useGetReleasePackageStatusWorkflowQuery(idOrKey, {
    skip: view !== 'diagram',
  })

  return (
    <RecordStatusHistory
      transitions={transitions}
      isLoading={isLoading}
      emptyDescription="No status changes have been recorded for this package."
      view={view}
      onViewChange={setView}
      workflow={workflow.data}
      isWorkflowLoading={workflow.isLoading}
      isWorkflowError={workflow.isError}
    />
  )
}

export default ReleasePackageStatusHistory
