'use client'

import { CheckCircleFilled, CloseCircleFilled } from '@ant-design/icons'
import { WaydTooltip } from '@/src/components/common'
import { formatInstantInZone } from '@/src/utils'
import { Steps } from 'antd'
import { ReactNode, useEffect, useRef, useState } from 'react'
import {
  IncidentStatusStep,
  IncidentStepStatus,
  incidentStatusSteps,
} from './incident-status-steps'
import { useGetIncidentStatusWorkflowQuery } from '@/src/store/features/product-management/incidents-api'
import styles from './incidents.module.css'

export interface IncidentStatusStepperProps {
  incidentId: string
  currentStatusId: string
  noticedAt?: Date | string | null
  startedAt?: Date | string | null
  recoveredAt?: Date | string | null
  /** The viewer's zone, so a stored instant reads as local time. */
  viewerZone: string
}

/**
 * Same widths as the project stage stepper. A step keeps its time at 120px,
 * keeps its name at 70px, and the page stacks under 500px.
 */
const DEFAULT_WIDTH_PER_STEP = 120
const VERTICAL_WIDTH_PER_STEP = 70
const PAGE_VERTICAL_THRESHOLD = 500

type DisplayMode = 'default' | 'small' | 'vertical'

function getDisplayMode(containerWidth: number, stepCount: number): DisplayMode {
  if (window.innerWidth < PAGE_VERTICAL_THRESHOLD) return 'vertical'
  if (containerWidth >= stepCount * DEFAULT_WIDTH_PER_STEP) return 'default'
  if (containerWidth >= stepCount * VERTICAL_WIDTH_PER_STEP) return 'small'
  return 'vertical'
}

function progressLabel(status: IncidentStepStatus): string {
  switch (status) {
    case 'finish':
      return 'Completed'
    case 'process':
      return 'In Progress'
    case 'error':
      return 'Canceled'
    default:
      return 'Not Started'
  }
}

function buildTooltip(step: IncidentStatusStep, mode: DisplayMode, when: string | null) {
  const label = progressLabel(step.status)
  if (mode !== 'small') return label
  return (
    <div>
      <div>{label}</div>
      {when && <div>{when}</div>}
    </div>
  )
}

function stepIcon(status: IncidentStepStatus, tooltip: ReactNode) {
  switch (status) {
    case 'finish':
      return (
        <WaydTooltip title={tooltip}>
          <CheckCircleFilled className={styles.iconCompleted} />
        </WaydTooltip>
      )
    case 'process':
      return (
        <WaydTooltip title={tooltip}>
          <span className={styles.dotInProgress} />
        </WaydTooltip>
      )
    case 'error':
      return (
        <WaydTooltip title={tooltip}>
          <CloseCircleFilled className={styles.iconCanceled} />
        </WaydTooltip>
      )
    default:
      return (
        <WaydTooltip title={tooltip}>
          <span className={styles.dotNotStarted} />
        </WaydTooltip>
      )
  }
}

/**
 * The statuses of the workflow this incident holds, drawn the way a project's
 * stages are: the name under the node, the stored time under the name, and
 * the time hidden when the row is too narrow to keep it.
 */
const IncidentStatusStepper = ({
  incidentId,
  currentStatusId,
  noticedAt,
  startedAt,
  recoveredAt,
  viewerZone,
}: IncidentStatusStepperProps) => {
  const { data } = useGetIncidentStatusWorkflowQuery(incidentId)
  const containerRef = useRef<HTMLDivElement>(null)
  const [mode, setMode] = useState<DisplayMode>('default')
  const steps = incidentStatusSteps(data?.statuses ?? [], currentStatusId, {
    noticedAt,
    startedAt,
    recoveredAt,
  })

  useEffect(() => {
    const el = containerRef.current
    if (!el) return

    const observer = new ResizeObserver((entries) => {
      const width = entries[0]?.contentRect.width ?? 0
      setMode(getDisplayMode(width, steps.length))
    })

    observer.observe(el)
    return () => observer.disconnect()
  }, [steps.length])

  if (steps.length === 0) return null

  const isVertical = mode === 'vertical'
  const items = steps.map((step) => {
    const when = step.at ? formatInstantInZone(step.at, viewerZone) : null
    const tooltip = buildTooltip(step, mode, when)
    return {
      title: (
        <WaydTooltip title={tooltip}>
          <span className={mode === 'small' ? styles.titleSmall : undefined}>{step.title}</span>
        </WaydTooltip>
      ),
      content:
        mode === 'small' || !when ? undefined : <div className={styles.dates}>{when}</div>,
      status: step.status,
      icon: stepIcon(step.status, tooltip),
    }
  })

  return (
    <div ref={containerRef} className={styles.stepper}>
      <Steps
        items={items}
        size={mode === 'default' ? 'medium' : 'small'}
        orientation={isVertical ? 'vertical' : 'horizontal'}
        titlePlacement={isVertical ? undefined : 'vertical'}
        responsive={false}
      />
    </div>
  )
}

export default IncidentStatusStepper
