'use client'

import {
  RecordStatusHistory,
  type StatusHistoryView,
} from '@/src/components/common/status-workflows'
import {
  useGetIntakeRequestStatusHistoryQuery,
  useGetIntakeRequestStatusWorkflowQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { FC, useState } from 'react'

export interface IntakeRequestStatusHistoryProps {
  requestId: string
}

/**
 * A request's status history, as a timeline or drawn on its desk's workflow. Fetched by id, the tag every
 * status move invalidates. A reader without the request's detail gets each change without its reason or
 * who made it; the server leaves them out.
 */
const IntakeRequestStatusHistory: FC<IntakeRequestStatusHistoryProps> = ({
  requestId,
}) => {
  const [view, setView] = useState<StatusHistoryView>('timeline')
  const { data: transitions, isLoading } =
    useGetIntakeRequestStatusHistoryQuery(requestId)
  const workflow = useGetIntakeRequestStatusWorkflowQuery(requestId, {
    skip: view !== 'diagram',
  })

  return (
    <RecordStatusHistory
      transitions={transitions}
      isLoading={isLoading}
      emptyDescription="No status changes have been recorded for this request."
      view={view}
      onViewChange={setView}
      workflow={workflow.data}
      isWorkflowLoading={workflow.isLoading}
      isWorkflowError={workflow.isError}
    />
  )
}

export default IntakeRequestStatusHistory
