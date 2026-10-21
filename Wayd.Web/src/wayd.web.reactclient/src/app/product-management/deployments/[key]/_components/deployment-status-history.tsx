'use client'

import {
  RecordStatusHistory,
  type StatusHistoryView,
} from '@/src/components/common/status-workflows'
import { StatusTransitionDto } from '@/src/services/wayd-api'
import { useGetDeploymentStatusWorkflowQuery } from '@/src/store/features/product-management/deployments-api'
import { useState } from 'react'

export interface DeploymentStatusHistoryProps {
  /** The key the page was routed with, which the history is fetched and tagged by too. */
  idOrKey: string
  transitions: StatusTransitionDto[] | undefined
  isLoading: boolean
}

/** A deployment's status history, as a timeline or drawn on its workflow. */
const DeploymentStatusHistory = ({
  idOrKey,
  transitions,
  isLoading,
}: DeploymentStatusHistoryProps) => {
  const [view, setView] = useState<StatusHistoryView>('timeline')
  const workflow = useGetDeploymentStatusWorkflowQuery(idOrKey, {
    skip: view !== 'diagram',
  })

  return (
    <RecordStatusHistory
      transitions={transitions}
      isLoading={isLoading}
      emptyDescription="No status changes have been recorded for this deployment."
      view={view}
      onViewChange={setView}
      workflow={workflow.data}
      isWorkflowLoading={workflow.isLoading}
      isWorkflowError={workflow.isError}
    />
  )
}

export default DeploymentStatusHistory
