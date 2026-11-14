'use client'

import { incidentStatusSteps } from '@/src/app/product-management/incidents/_components/incident-status-steps'
import { useGetIncidentStatusWorkflowQuery } from '@/src/store/features/product-management/incidents-api'
import { Steps } from 'antd'
import type { CSSProperties } from 'react'

/**
 * Titles stay one line. Beside the facts rail the record column is narrower
 * than the status names, and a title that shrinks breaks mid-word. The row
 * scrolls instead of compressing them.
 */
const stepStyles: {
  root: CSSProperties
  item: CSSProperties
  itemSection: CSSProperties
  itemTitle: CSSProperties
} = {
  root: { minWidth: 0, maxWidth: '100%', overflowX: 'auto' },
  item: { flex: '1 0 max-content', minWidth: 'max-content' },
  itemSection: { minWidth: 'max-content' },
  itemTitle: { flexShrink: 0, whiteSpace: 'nowrap', wordBreak: 'normal' },
}

export interface IncidentStatusStepperProps {
  incidentId: string
  currentStatusId: string
}

/**
 * The statuses of the workflow this incident holds, in that workflow's order.
 *
 * The current status is the marked step. Canceled is one of those statuses, so it
 * stays where the workflow put it and is marked only while the incident holds it.
 */
const IncidentStatusStepper = ({
  incidentId,
  currentStatusId,
}: IncidentStatusStepperProps) => {
  const { data } = useGetIncidentStatusWorkflowQuery(incidentId)
  const steps = incidentStatusSteps(data?.statuses ?? [], currentStatusId)
  if (steps.length === 0) return null

  const current = steps.findIndex((step) => step.status === 'process')

  return (
    <Steps
      size="small"
      responsive={false}
      current={current === -1 ? undefined : current}
      items={steps}
      styles={stepStyles}
    />
  )
}

export default IncidentStatusStepper
