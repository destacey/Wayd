'use client'

import {
  RecordStatusHistory,
  type StatusHistoryView,
} from '@/src/components/common/status-workflows'
import { StatusTransitionDto } from '@/src/services/wayd-api'
import { useGetVersionStatusWorkflowQuery } from '@/src/store/features/product-management/versions-api'
import { useState } from 'react'

export interface VersionStatusHistoryProps {
  /** The key the page was routed with, which the history is fetched and tagged by too. */
  idOrKey: string
  transitions: StatusTransitionDto[] | undefined
  isLoading: boolean
}

/** A version's status history, as a timeline or drawn on its workflow. */
const VersionStatusHistory = ({
  idOrKey,
  transitions,
  isLoading,
}: VersionStatusHistoryProps) => {
  const [view, setView] = useState<StatusHistoryView>('timeline')
  const workflow = useGetVersionStatusWorkflowQuery(idOrKey, {
    skip: view !== 'diagram',
  })

  return (
    <RecordStatusHistory
      transitions={transitions}
      isLoading={isLoading}
      emptyDescription="No status changes have been recorded for this version."
      view={view}
      onViewChange={setView}
      workflow={workflow.data}
      isWorkflowLoading={workflow.isLoading}
      isWorkflowError={workflow.isError}
    />
  )
}

export default VersionStatusHistory
